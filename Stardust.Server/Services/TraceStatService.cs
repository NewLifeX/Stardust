using System.Collections.Concurrent;
using NewLife;
using NewLife.Log;
using NewLife.Threading;
using Stardust.Data.Monitors;
using XCode;
using XCode.Model;

namespace Stardust.Server.Services;

/// <summary>追踪统计服务。定义追踪数据统计的入口，支持批量添加和按应用+时间统计</summary>
public interface ITraceStatService
{
    /// <summary>添加需要统计的追踪数据</summary>
    /// <param name="traces">追踪数据列表</param>
    void Add(IList<TraceData> traces);

    /// <summary>统计特定应用和时间</summary>
    /// <param name="appId">应用编号</param>
    /// <param name="time">统计时间</param>
    void Add(Int32 appId, DateTime time);

    /// <summary>设置热门应用。监控中心正在查看的应用，缩短计算周期以提供更实时的统计数据</summary>
    /// <param name="appId">应用编号</param>
    void SetHotApp(Int32 appId);

    /// <summary>获取内存中的分钟统计。告警实时判定使用，避免等待延迟队列落盘；未命中返回null，由数据库兜底</summary>
    /// <param name="appId">应用编号</param>
    /// <param name="itemId">跟踪项编号</param>
    /// <param name="time">统计时间（5分钟桶）</param>
    /// <returns>内存中的统计对象</returns>
    TraceMinuteStat GetLiveMinute(Int32 appId, Int32 itemId, DateTime time);

    /// <summary>获取内存中的应用分钟统计。告警实时判定使用，避免等待延迟队列落盘；未命中返回null，由数据库兜底</summary>
    /// <param name="appId">应用编号</param>
    /// <param name="time">统计时间（5分钟桶）</param>
    /// <returns>内存中的统计对象</returns>
    AppMinuteStat GetLiveAppMinute(Int32 appId, DateTime time);

    /// <summary>紧急落盘。触发分钟级统计队列立即写入数据库，保证告警依据及时入库</summary>
    void TriggerFlush();

    /// <summary>清空并处理全部缓存，用于优雅停机，缩小重启数据缺口</summary>
    void Flush();
}

/// <summary>追踪统计服务</summary>
/// <remarks>
/// 统计分三段，通过热门窗口与巡检在“实时性”和“写压力”之间平衡：
/// 1. 告警链：分钟级统计全量流式，告警直接读内存实时值，落盘可以稀疏（SavePeriod）；
/// 2. 实时链：热门应用（正被查看）的小时/日统计由数据上报驱动实时重算，打开页面即时刷新；
/// 3. 巡检链：非热门应用的小时/日统计由巡检按 PatrolPeriod 兜底重算，小时切换后延迟一轮收尾上一小时，补齐巡检间隙数据；
/// 进程首次巡检兼作启动补算，优雅停机时 Flush 落盘，保证重启不丢统计。
/// </remarks>
public class TraceStatService : ITraceStatService
{
    /// <summary>流计算周期。默认5秒</summary>
    /// <remarks>
    /// 处理内存队列中的追踪数据，计算分钟级统计（告警数据源）。
    /// 调小：实时性更好，告警延迟低，但CPU占用高。
    /// 调大：CPU占用低，但告警延迟增加（队列积压一般小于1000条，可适当调大到30秒）。
    /// </remarks>
    public Int32 FlowPeriod { get; set; } = 5;

    /// <summary>批计算周期。默认30秒</summary>
    /// <remarks>
    /// 从数据库重新聚合数据，修正流式计算的偏差，计算小时/天级统计。
    /// 调小：数据修正更及时，但CPU和数据库IO压力增大。
    /// 调大：减少CPU和IO压力（告警容忍3-5分钟延迟，可适当调大到60秒）。
    /// </remarks>
    public Int32 BatchPeriod { get; set; } = 30;

    /// <summary>落盘保存周期。默认300秒</summary>
    /// <remarks>
    /// 延迟队列批量提交统计数据到数据库的周期，是数据库写压力的主要来源。
    /// 告警读取内存实时值，查看页面会触发即时落盘，因此可以放宽落盘而几乎不影响体验。
    /// 调小：数据持久化更快，系统重启时数据丢失少，但数据库UPDATE压力大。
    /// 调大：大幅减少数据库UPDATE次数，降低IO压力，但重启时可能丢失更多内存数据。
    /// </remarks>
    public Int32 SavePeriod { get; set; } = 300;

    /// <summary>统计巡检周期（秒）。默认600秒</summary>
    /// <remarks>非热门应用由巡检按本周期重算当前小时与当天统计，替代每个批周期的全量重算，显著降低写放大；小时切换后由下一轮巡检收尾上一小时</remarks>
    public Int32 PatrolPeriod { get; set; } = 600;

    /// <summary>启用启动恢复补算。默认true。仅接收数据上报的进程需要，用于补上重启丢失的内存增量；纯查看端进程设为false，避免与数据进程重复重算</summary>
    public Boolean EnableRecovery { get; set; } = true;

    /// <summary>热门应用有效窗口（秒）。查看页面后该窗口内保持流式实时统计</summary>
    private const Int32 HotWindow = 600;

    /// <summary>统计巡检的下次执行时间</summary>
    private DateTime _nextPatrol = DateTime.MinValue;

    /// <summary>上次巡检所在的小时桶。用于检测小时切换</summary>
    private DateTime _lastPatrolHour = DateTime.MinValue;

    /// <summary>待收尾的小时统计。小时切换后登记，下一轮巡检执行，等待分钟统计先按周期落盘</summary>
    private DateTime _pendingHour = DateTime.MinValue;

    private TimerX _timerFlow;
    private TimerX _timerBatch;
    private readonly ConcurrentBag<String> _bagDay = [];
    private readonly ConcurrentBag<String> _bagHour = [];
    private readonly ConcurrentDictionary<String, ConcurrentBag<DateTime>> _bagMinute = new();
    private readonly ConcurrentQueue<TraceData> _queue = new();

    /// <summary>当日活跃应用。Key=应用Id_日期，Value=最近活跃时间，用于非热门应用巡检重算与跨天收敛</summary>
    private readonly ConcurrentDictionary<String, DateTime> _dayActive = [];

    /// <summary>热门应用（监控中心正在查看）。Key=应用Id，Value=最近查看时间，窗口内该应用日统计保持流式实时</summary>
    private readonly ConcurrentDictionary<Int32, DateTime> _hotApps = [];

    /* 延迟队列技术，周期越长则批量越大，UPDATE次数越少 */
    private DayQueue _dayQueue;
    private HourQueue _hourQueue;
    private MinuteQueue _minuteQueue;
    private AppMinuteQueue _appMinuteQueue;

    private Int32 _count;
    private readonly ITracer _tracer;

    /// <summary>实例化追踪统计服务</summary>
    /// <param name="tracer">跟踪器</param>
    public TraceStatService(ITracer tracer) => _tracer = tracer;

    /// <summary>添加需要统计的追踪数据</summary>
    /// <param name="traces">追踪数据列表</param>
    public void Add(IList<TraceData> traces)
    {
        if (traces == null || traces.Count == 0) return;

        // 放入队列包，凑批处理
        foreach (var item in traces)
        {
            // 当天活跃登记。非热门应用由巡检按周期统一重算，这里是巡检的活跃应用来源
            var key = $"{item.AppId}_{item.StatDate.ToFullString()}";
            _dayActive[key] = DateTime.Now;

            // 热门应用（监控中心正在查看）保持事件驱动的实时重算；
            // 非热门应用不登记小时/日包，交由巡检按 PatrolPeriod 兜底重算，消除每个批周期全量重算的写放大
            if (IsHotApp(item.AppId))
            {
                if (!_bagDay.Contains(key)) _bagDay.Add(key);

                var hkey = $"{item.AppId}_{item.StatHour.ToFullString()}";
                if (!_bagHour.Contains(hkey)) _bagHour.Add(hkey);
            }

            {
                var mkey = $"{item.AppId}_{item.StatMinute:yyyyMMddHH}";
                var bag = _bagMinute.GetOrAdd(mkey, []);
                if (!bag.Contains(item.StatMinute)) bag.Add(item.StatMinute);
            }
        }

        // 限制增量队列长度，避免内存暴涨。过多数据留给定时批处理
        if (_count > 100_000) return;

        // 加入队列，增量计算
        foreach (var item in traces)
        {
            _queue.Enqueue(item);
            Interlocked.Increment(ref _count);
        }

        // 初始化定时器，用于流式增量计算和批量计算
        Init();
    }

    /// <summary>统计特定应用和时间</summary>
    /// <param name="appId">应用编号</param>
    /// <param name="time">统计时间</param>
    public void Add(Int32 appId, DateTime time)
    {
        Init();

        {
            var key = $"{appId}_{time.Date.ToFullString()}";
            if (!_bagDay.Contains(key)) _bagDay.Add(key);
            _dayActive[key] = DateTime.Now;
        }
        {
            var hour = time.Date.AddHours(time.Hour);
            var key = $"{appId}_{hour.ToFullString()}";
            if (!_bagHour.Contains(key)) _bagHour.Add(key);
        }
        {
            var minute = time.Date.AddHours(time.Hour).AddMinutes(time.Minute / 5 * 5);
            var key = $"{appId}_{minute:yyyyMMddHH}";
            var bag = _bagMinute.GetOrAdd(key, []);
            if (!bag.Contains(minute)) bag.Add(minute);
        }

        _timerBatch?.SetNext(3_000);
    }

    /// <summary>初始化定时器</summary>
    public void Init()
    {
        if (_timerFlow == null && FlowPeriod > 0)
        {
            lock (this)
            {
                _timerFlow ??= new TimerX(DoFlowStat, null, 5_000, FlowPeriod * 1000) { Async = true };
            }
        }
        if (_timerBatch == null && BatchPeriod > 0)
        {
            lock (this)
            {
                _timerBatch ??= new TimerX(DoBatchStat, null, 5_000, BatchPeriod * 1000) { Async = true };
            }
        }
    }

    /// <summary>设置热门应用。监控中心正在查看的应用，缩短计算周期以提供更实时的统计数据</summary>
    /// <param name="appId">应用编号</param>
    public void SetHotApp(Int32 appId)
    {
        if (appId <= 0) return;

        Init();

        // 记录热门应用与最近查看时间，作为统计实时化的热窗口
        _hotApps[appId] = DateTime.Now;

        // 打开页面时立即登记重算（分钟槽+当前小时+当天），并缩短批计算周期尽快处理
        Add(appId, DateTime.Now);

        // 触发各延迟队列立即落盘
        _minuteQueue?.Trigger();
        _hourQueue?.Trigger();
        _dayQueue?.Trigger();
        _appMinuteQueue?.Trigger();
    }

    /// <summary>是否热门应用。仅在有效窗口内返回true，窗口内保持流式实时日统计</summary>
    /// <param name="appId">应用编号</param>
    /// <returns>是否热门</returns>
    private Boolean IsHotApp(Int32 appId)
    {
        if (_hotApps.TryGetValue(appId, out var time))
        {
            if (DateTime.Now - time <= TimeSpan.FromSeconds(HotWindow)) return true;

            // 窗口过期，移出热集合
            _hotApps.TryRemove(appId, out _);
        }
        return false;
    }

    /// <summary>流式计算，增量累加</summary>
    /// <remarks>
    /// 分钟级与应用分钟级全量增量累加（告警数据源），小时/日级仅热门应用；
    /// 其余小时/日统计由批算与巡检覆盖重算，小时切换后由巡检收尾上一小时。
    /// </remarks>
    /// <param name="state">定时器状态参数</param>
    private void DoFlowStat(Object state)
    {
        if (_queue.IsEmpty) return;

        using var span = _tracer?.NewSpan("TraceFlowStat", new { queue = _count });

        // 限制每次只处理这么多
        var count = 100_000;
        while (count-- > 0)
        {
            if (!_queue.TryDequeue(out var td)) break;
            Interlocked.Decrement(ref _count);

            // 过滤异常数据
            if (td.AppId <= 0 || td.Name.IsNullOrEmpty()) continue;

            // 热门门控：仅热门应用（监控中心正在查看）保留小时/日统计的流式实时累加，
            // 非热门应用由巡检按 PatrolPeriod 兜底重算，显著降低统计写放大
            var hot = IsHotApp(td.AppId);

            // 每日。仅热门应用保留流式实时日统计
            if (hot)
            {
                var queue = _dayQueue ??= new();
                queue.Period = SavePeriod * 1000;
                var st = queue.GetOrAdd(td.StatDate, td.AppId, td.ItemId, out var key);

                st.Total += td.Total;
                st.Errors += td.Errors;
                st.TotalCost += td.TotalCost;
                if (st.MaxCost < td.MaxCost) st.MaxCost = td.MaxCost;
                if (st.MinCost <= 0 || st.MinCost > td.MinCost && td.MinCost > 0) st.MinCost = td.MinCost;
                st.TotalValue += td.TotalValue;

                queue.Commit(key);
            }

            // 小时。仅热门应用保留流式实时小时统计，非热门应用由巡检在小时切换后收尾重算
            if (hot)
            {
                var queue = _hourQueue ??= new();
                queue.Period = SavePeriod * 1000;
                var st = queue.GetOrAdd(td.StatHour, td.AppId, td.ItemId, out var key);

                st.Total += td.Total;
                st.Errors += td.Errors;
                st.TotalCost += td.TotalCost;
                if (st.MaxCost < td.MaxCost) st.MaxCost = td.MaxCost;
                if (st.MinCost <= 0 || st.MinCost > td.MinCost && td.MinCost > 0) st.MinCost = td.MinCost;
                st.TotalValue += td.TotalValue;

                queue.Commit(key);
            }

            // 分钟统计（告警数据源，需要较高实时性）
            {
                var queue = _minuteQueue ??= new();
                queue.Period = SavePeriod * 1000;
                var st = queue.GetOrAdd(td.StatMinute, td.AppId, td.ItemId, out var key);

                st.Total += td.Total;
                st.Errors += td.Errors;
                st.TotalCost += td.TotalCost;
                if (st.MaxCost < td.MaxCost) st.MaxCost = td.MaxCost;
                if (st.MinCost <= 0 || st.MinCost > td.MinCost && td.MinCost > 0) st.MinCost = td.MinCost;
                st.TotalValue += td.TotalValue;

                queue.Commit(key);
            }

            // 应用分钟统计（告警数据源，需要较高实时性）
            {
                var queue = _appMinuteQueue ??= new();
                queue.Period = SavePeriod * 1000;
                var st = queue.GetOrAdd(td.StatMinute, td.AppId, out var key);

                st.Total += td.Total;
                st.Errors += td.Errors;
                st.TotalCost += td.TotalCost;
                if (st.MaxCost < td.MaxCost) st.MaxCost = td.MaxCost;
                if (st.MinCost <= 0 || st.MinCost > td.MinCost && td.MinCost > 0) st.MinCost = td.MinCost;

                queue.Commit(key);
            }

            if (span != null) span.Value++;
        }

        // 更新周期
        if (FlowPeriod > 0 && _timerFlow != null) _timerFlow.Period = FlowPeriod * 1000;
    }

    /// <summary>批计算，覆盖缺失</summary>
    /// <param name="state"></param>
    private void DoBatchStat(Object state)
    {
        var keys = _bagMinute.Keys;
        foreach (var item in keys)
        {
            // 摘取下来
            if (_bagMinute.TryRemove(item, out var bag))
            {
                var ss = item.Split("_");
                var appId = ss[0].ToInt();
                var list = new List<DateTime>();
                while (bag.TryTake(out var dt))
                {
                    if (!list.Contains(dt)) list.Add(dt);
                }

                // 批量处理该应用，取最小时间和最大时间
                if (list.Count > 0) ProcessMinute(appId, list.Min(), list.Max());
            }
        }

        // 休息5000ms，让分钟统计落库
        Thread.Sleep(5000);

        while (_bagHour.TryTake(out var key))
        {
            var ss = key.Split("_");
            ProcessHour(ss[0].ToInt(), ss[1].ToDateTime());
        }
        while (_bagDay.TryTake(out var key))
        {
            var ss = key.Split("_");
            ProcessDay(ss[0].ToInt(), ss[1].ToDateTime());
        }

        // 巡检：非热门应用按周期重算当前小时与当天统计，历史日跨天后补算最终值
        DoPatrol();

        // 更新周期
        if (BatchPeriod > 0 && _timerBatch != null) _timerBatch.Period = BatchPeriod * 1000;
    }

    /// <summary>统计巡检。非热门应用按周期重算当前小时与当天统计；小时切换后延迟一轮收尾上一小时；历史日（跨天）补算最终值后收敛移除登记。
    /// 数据上报进程首次巡检兼作启动补算，额外重算最近窗口的分钟统计，覆盖重启丢失的内存增量</summary>
    private void DoPatrol()
    {
        var now = DateTime.Now;
        if (now < _nextPatrol) return;

        // 防护：巡检周期误配为非正数时回退默认周期，避免每个批周期都巡检造成写放大
        var period = PatrolPeriod > 0 ? PatrolPeriod : 600;

        // 巡检活跃窗口，取巡检周期3倍，同时保证首次巡检的启动补算覆盖足够窗口
        var window = TimeSpan.FromSeconds(period * 3);

        // 进程首次巡检（_nextPatrol 仍为初始值）兼作启动补算。仅数据上报进程需要，补上重启丢失的内存增量
        var recovery = EnableRecovery && _nextPatrol == DateTime.MinValue;

        // 每巡检周期节流一次
        _nextPatrol = now.AddSeconds(period);

        // 取出上一轮登记的收尾小时。延迟一轮执行，确保分钟统计已按落盘周期写入数据库
        var pending = _pendingHour;
        _pendingHour = DateTime.MinValue;

        // 清理过期的热门应用窗口，防止字典无限增长
        // IsHotApp 仅在该应用仍有数据上报时才被调用，停报应用需在此统一回收
        foreach (var item in _hotApps)
        {
            if (now - item.Value > TimeSpan.FromSeconds(HotWindow))
                _hotApps.TryRemove(item.Key, out _);
        }

        var today = now.Date;
        var hour = today.AddHours(now.Hour);

        // 检测小时切换，登记到下一轮巡检收尾。非热门应用的小时统计不参与流式累加，
        // 由收尾从分钟统计全量重算，覆盖巡检间隙的最后一段数据
        if (_lastPatrolHour.Year > 2000 && hour != _lastPatrolHour)
            _pendingHour = _lastPatrolHour;
        else if (recovery && _lastPatrolHour.Year <= 2000)
            _pendingHour = hour.AddHours(-1);
        _lastPatrolHour = hour;

        // 执行小时收尾。逐应用从分钟统计重算上一小时，随后立即落盘，
        // 保证紧接着的跨天日终聚合能够读到最终的小时统计
        if (pending.Year > 2000)
        {
            foreach (var item in _dayActive)
            {
                // 单个应用异常不影响其余应用
                try
                {
                    var ss = item.Key.Split("_");
                    if (ss.Length < 2) continue;

                    var appId = ss[0].ToInt();
                    var date = ss[1].ToDateTime();
                    if (appId <= 0 || date.Year < 2000) continue;

                    // 仅处理该小时所在日期，且该小时内仍有活跃的应用
                    if (date.Date != pending.Date) continue;
                    if (item.Value < pending) continue;

                    ProcessHour(appId, pending);
                }
                catch (Exception ex)
                {
                    XTrace.WriteException(ex);
                }
            }

            _hourQueue?.Flush();
        }

        foreach (var item in _dayActive)
        {
            // 单个应用异常不影响其余应用的巡检
            try
            {
                var ss = item.Key.Split("_");
                if (ss.Length < 2) continue;

                var appId = ss[0].ToInt();
                var date = ss[1].ToDateTime();
                if (appId <= 0 || date.Year < 2000) continue;

                // 热门应用由流式实时维护，跳过巡检
                if (IsHotApp(appId)) continue;

                if (date < today)
                {
                    // 小时收尾尚未执行（跨天首个巡检）时推迟日终补算，
                    // 等待下一轮收尾落盘后再聚合，避免读到未收尾的小时行
                    if (_pendingHour.Year > 2000 && _pendingHour.Date == date) continue;

                    // 历史日：由小时级统计全量聚合补算最终值后收敛移除
                    ProcessDay(appId, date);
                    _dayActive.TryRemove(item.Key, out _);
                }
                else if (recovery || now - item.Value <= window)
                {
                    // 首次巡检：从原始数据补算最近窗口的分钟统计，覆盖重启丢失的内存增量
                    if (recovery) ProcessMinute(appId, now - window, now);

                    // 当天且近期活跃：重算当前小时与当天统计，保证缓慢跟上
                    ProcessHour(appId, hour);
                    ProcessDay(appId, today);
                }
                // 当天已不活跃：不再重算空转，但保留登记等待跨天后补算最终值，避免当日尾部数据缺失
            }
            catch (Exception ex)
            {
                XTrace.WriteException(ex);
            }
        }

        // 补算结果立即落盘，缩短重启后的数据盲区
        if (recovery) TriggerFlush();
    }

    private void ProcessDay(Int32 appId, DateTime time)
    {
        if (appId <= 0 || time.Year < 2000) return;

        var date = time.Date;
        using var span = _tracer?.NewSpan("TraceBatchStat-Day", new { appId, time });

        // 统计数据。由小时级汇总
        var list = TraceHourStat.FindAllByAppIdWithCache(appId, date, date.AddDays(1));
        if (list.Count == 0) return;

        span?.AppendTag($"TraceHourStat.Count={list.Count}");

        // 昨日统计
        var sts2 = TraceDayStat.FindAllByAppIdWithCache(appId, date.AddDays(-1));
        span?.AppendTag($"Yesterday={sts2.Count}");

        var queue = _dayQueue ??= new();
        queue.Period = SavePeriod * 1000;

        // 聚合
        // 分组聚合，这里包含了每个接口在该日内的所有分钟统计，需要求和
        foreach (var item in list.GroupBy(e => e.ItemId))
        {
            if (item.Key == 0) continue;

            var st = queue.GetOrAdd(date, appId, item.Key, out var key);

            var vs = item.ToList();
            st.Total = vs.Sum(e => e.Total);
            st.Errors = vs.Sum(e => e.Errors);
            st.TotalCost = vs.Sum(e => e.TotalCost);
            st.MaxCost = vs.Max(e => e.MaxCost);
            var vs2 = vs.Where(e => e.MinCost > 0).ToList();
            if (vs2.Count > 0) st.MinCost = vs2.Min(e => e.MinCost);
            st.TotalValue = vs.Sum(e => e.TotalValue);

            // 计算TP99
            if (st.Total >= 50)
            {
                var totalCost = st.TotalCost;
                var ms = vs.Select(e => e.MaxCost).OrderByDescending(e => e).ToList();
                var n = (Int32)Math.Round(st.Total * 0.01);
                var i = 0;
                for (i = 0; i < n && i < ms.Count; i++)
                {
                    totalCost -= ms[i];
                }

                // 重新计算平均耗时，去掉了头部1%的最大值
                if (totalCost > 0)
                {
                    st.Cost = (Int32)Math.Round((Double)totalCost / (st.Total - i));
                }
            }

            // 计算种类
            if (st.Type.IsNullOrEmpty() || st.Name.IsNullOrEmpty())
            {
                var ti = TraceItem.FindById(item.Key);
                if (ti != null)
                {
                    st.Type = ti.Kind;
                    st.Name = ti + "";
                }
            }

            // 计算环比
            var st2 = sts2.FirstOrDefault(e => e.ItemId == item.Key);
            if (st2 != null) st.RingRate = st2.Total <= 0 ? 1 : Math.Round((Double)st.Total / st2.Total, 4);

            //// 强制触发种类计算
            //st.Valid(false);

            queue.Commit(key);

            if (span != null) span.Value++;
        }
    }

    private void ProcessHour(Int32 appId, DateTime time)
    {
        if (appId <= 0 || time.Year < 2000) return;

        using var span = _tracer?.NewSpan("TraceBatchStat-Hour", new { appId, time });
        time = time.Date.AddHours(time.Hour);

        // 统计数据。分钟级统计可能因埋点名称污染，导致产生大量数据，这里过滤要最大1000行
        var list = TraceMinuteStat.FindAllByAppIdWithCache(appId, time, time.AddHours(1), 24 * 60 / 5 * 1000);
        list = list.Where(e => e.StatTime >= time && e.StatTime < time.AddHours(1)).ToList();
        if (list.Count == 0) return;

        span?.AppendTag($"TraceMinuteStat.Count={list.Count}");

        // 昨日统计
        var sts2 = TraceHourStat.FindAllByAppIdWithCache(appId, time.AddDays(-1), time.AddDays(-1).AddHours(1));
        span?.AppendTag($"Yesterday={sts2.Count}");

        var queue = _hourQueue ??= new();
        queue.Period = SavePeriod * 1000;

        // 分组聚合，这里包含了每个接口在该小时内的所有分钟统计，需要求和
        foreach (var item in list.GroupBy(e => e.ItemId))
        {
            if (item.Key == 0) continue;

            var st = queue.GetOrAdd(time, appId, item.Key, out var key);

            var vs = item.ToList();
            st.Total = vs.Sum(e => e.Total);
            st.Errors = vs.Sum(e => e.Errors);
            st.TotalCost = vs.Sum(e => e.TotalCost);
            st.MaxCost = vs.Max(e => e.MaxCost);
            var vs2 = vs.Where(e => e.MinCost > 0).ToList();
            if (vs2.Count > 0) st.MinCost = vs2.Min(e => e.MinCost);
            st.TotalValue = vs.Sum(e => e.TotalValue);

            // 计算TP99
            if (st.Total >= 50)
            {
                var totalCost = st.TotalCost;
                var ms = vs.Select(e => e.MaxCost).OrderByDescending(e => e).ToList();
                var n = (Int32)Math.Round(st.Total * 0.01);
                var i = 0;
                for (i = 0; i < n && i < ms.Count; i++)
                {
                    totalCost -= ms[i];
                }

                // 重新计算平均耗时，去掉了头部1%的最大值
                if (totalCost > 0)
                {
                    st.Cost = (Int32)Math.Round((Double)totalCost / (st.Total - i));
                }
            }

            // 计算环比
            var st2 = sts2.FirstOrDefault(e => e.ItemId == item.Key);
            if (st2 != null) st.RingRate = st2.Total <= 0 ? 1 : Math.Round((Double)st.Total / st2.Total, 4);

            queue.Commit(key);

            if (span != null) span.Value++;
        }
    }

    private void ProcessMinute(Int32 appId, DateTime start, DateTime end)
    {
        if (appId <= 0 || start.Year < 2000 || end.Year < 2000) return;

        using var span = _tracer?.NewSpan("TraceBatchStat-Minute", $"{start.ToFullString()}-{end.ToFullString()}");

        // 排除项
        var app = AppTracer.FindByID(appId);
        var excludes = app.Excludes.Split(",", ";") ?? [];

        start = start.Date.AddHours(start.Hour).AddMinutes(start.Minute / 5 * 5);
        end = end.Date.AddHours(end.Hour).AddMinutes(end.Minute / 5 * 5);

        // 统计数据
        var list = TraceData.SearchGroupAppAndName(appId, start, end);
        list = list.Where(e => !e.Name.IsNullOrEmpty()).ToList();
        if (list.Count == 0) return;

        // 剔除指定项
        list = list.Where(e => !e.Name.IsNullOrEmpty() && !excludes.Any(y => y.IsMatch(e.Name, StringComparison.OrdinalIgnoreCase))).ToList();

        var queue = _minuteQueue ??= new();
        queue.Period = SavePeriod * 1000;

        // 聚合
        foreach (var item in list)
        {
            var st = queue.GetOrAdd(item.StatMinute, appId, item.ItemId, out var key);

            st.Total = item.Total;
            st.Errors = item.Errors;
            st.TotalCost = item.TotalCost;
            st.MaxCost = item.MaxCost;
            st.MinCost = item.MinCost;
            st.TotalValue = item.TotalValue;

            queue.Commit(key);
        }

        var queue2 = _appMinuteQueue ??= new();
        queue2.Period = SavePeriod * 1000;

        // 聚合应用分钟统计
        foreach (var item in list.GroupBy(e => e.AppId + "#" + e.StatMinute))
        {
            var traces = item.ToList();
            var st = queue2.GetOrAdd(traces[0].StatMinute, traces[0].AppId, out var key);

            st.Total = traces.Sum(e => e.Total);
            st.Errors = traces.Sum(e => e.Errors);
            st.TotalCost = traces.Sum(e => e.TotalCost);
            st.MaxCost = traces.Max(e => e.MaxCost);
            //st.MinCost = traces.Min(e => e.MinCost);
            var vs2 = traces.Where(e => e.MinCost > 0).ToList();
            if (vs2.Count > 0) st.MinCost = vs2.Min(e => e.MinCost);

            // 计算TP99
            if (st.Total >= 50)
            {
                //// 计算总耗时，如果某个跟踪项数据较多，则其平均值已被掐头
                //var totalCost = traces.Sum(e => e.Total >= 50 ? (e.Total - 1) * e.Cost : e.TotalCost);
                var totalCost = st.TotalCost;

                var ms = traces.Select(e => e.MaxCost).OrderByDescending(e => e).ToList();
                var n = (Int32)Math.Round(st.Total * 0.01);
                var i = 0;
                for (i = 0; i < n && i < ms.Count; i++)
                {
                    totalCost -= ms[i];
                }

                // 重新计算平均耗时，去掉了头部1%的最大值
                if (totalCost > 0)
                {
                    st.Cost = (Int32)Math.Round((Double)totalCost / (st.Total - i));
                }
            }

            queue2.Commit(key);

            if (span != null) span.Value++;
        }
    }

    /// <summary>获取内存中的分钟统计。告警实时判定使用，避免等待延迟队列落盘；未命中返回null，由数据库兜底</summary>
    /// <param name="appId">应用编号</param>
    /// <param name="itemId">跟踪项编号</param>
    /// <param name="time">统计时间（5分钟桶）</param>
    /// <returns>内存中的统计对象</returns>
    public TraceMinuteStat GetLiveMinute(Int32 appId, Int32 itemId, DateTime time)
    {
        var queue = _minuteQueue;
        if (queue == null) return null;

        var model = new TraceStatModel { Time = time, AppId = appId, ItemId = itemId };
        return queue.Entities.TryGetValue(model.Key, out var value) ? value as TraceMinuteStat : null;
    }

    /// <summary>获取内存中的应用分钟统计。告警实时判定使用，避免等待延迟队列落盘；未命中返回null，由数据库兜底</summary>
    /// <param name="appId">应用编号</param>
    /// <param name="time">统计时间（5分钟桶）</param>
    /// <returns>内存中的统计对象</returns>
    public AppMinuteStat GetLiveAppMinute(Int32 appId, DateTime time)
    {
        var queue = _appMinuteQueue;
        if (queue == null) return null;

        var model = new TraceStatModel { Time = time, AppId = appId };
        return queue.Entities.TryGetValue(model.Key, out var value) ? value as AppMinuteStat : null;
    }

    /// <summary>紧急落盘。触发分钟级统计队列立即写入数据库，保证告警依据及时入库</summary>
    public void TriggerFlush()
    {
        _minuteQueue?.Trigger();
        _appMinuteQueue?.Trigger();
    }

    /// <summary>清空并处理全部缓存，用于优雅停机，缩小重启数据缺口</summary>
    public void Flush()
    {
        _minuteQueue?.Flush();
        _appMinuteQueue?.Flush();
        _hourQueue?.Flush();
        _dayQueue?.Flush();
    }
}

internal class DayQueue : MyQueue
{
    public TraceDayStat GetOrAdd(DateTime date, Int32 appId, Int32 itemId, out String key)
    {
        var model = new TraceStatModel { Time = date, AppId = appId, ItemId = itemId };
        key = model.Key;
        return GetOrAdd(key, k => TraceDayStat.FindOrAdd(model));
    }
}

internal class HourQueue : MyQueue
{
    public TraceHourStat GetOrAdd(DateTime date, Int32 appId, Int32 itemId, out String key)
    {
        var model = new TraceStatModel { Time = date, AppId = appId, ItemId = itemId };
        key = model.Key;
        return GetOrAdd(key, k => TraceHourStat.FindOrAdd(model));
    }
}

internal class MinuteQueue : MyQueue
{
    public TraceMinuteStat GetOrAdd(DateTime date, Int32 appId, Int32 itemId, out String key)
    {
        var model = new TraceStatModel { Time = date, AppId = appId, ItemId = itemId };
        key = model.Key;
        return GetOrAdd(key, k => TraceMinuteStat.FindOrAdd(model));
    }
}

internal class AppMinuteQueue : MyQueue
{
    public AppMinuteStat GetOrAdd(DateTime date, Int32 appId, out String key)
    {
        var model = new TraceStatModel { Time = date, AppId = appId };
        key = model.Key;
        return GetOrAdd(key, k => AppMinuteStat.FindOrAdd(model));
    }
}

internal class MyQueue : EntityDeferredQueue
{
    #region 方法
    /// <summary>处理一批</summary>
    /// <param name="list"></param>
    public override Int32 Process(IList<Object> list)
    {
        if (list.Count == 0) return 0;

        return list.Cast<IEntity>().Update();
    }
    #endregion
}