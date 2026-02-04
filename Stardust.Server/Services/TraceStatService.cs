using System.Collections.Concurrent;
using NewLife;
using NewLife.Log;
using NewLife.Threading;
using Stardust.Data.Monitors;
using XCode;
using XCode.Model;

namespace Stardust.Server.Services;

/// <summary>追踪统计服务</summary>
public interface ITraceStatService
{
    /// <summary>添加需要统计的追踪数据</summary>
    /// <param name="traces"></param>
    void Add(IList<TraceData> traces);

    /// <summary>统计特定应用和时间</summary>
    /// <param name="appId"></param>
    /// <param name="time"></param>
    void Add(Int32 appId, DateTime time);
}

/// <summary>追踪统计服务</summary>
/// <remarks>
/// 性能优化说明：
/// 1. 流式计算周期从5秒调整为30秒，队列积压一般&lt;1000条，无需过于频繁
/// 2. 批量计算周期从30秒调整为60秒，告警延迟可容忍3-5分钟
/// 3. 延迟队列提交周期延长，减少数据库UPDATE压力（主要性能瓶颈）
/// 4. 分钟级统计周期较短（120秒），用于告警数据源
/// 5. 小时/天级统计周期较长（180秒），这些数据实时性要求低
/// </remarks>
public class TraceStatService : ITraceStatService
{
    /// <summary>流计算周期。默认30秒</summary>
    public Int32 FlowPeriod { get; set; } = 30;

    /// <summary>批计算周期。默认60秒</summary>
    public Int32 BatchPeriod { get; set; } = 60;

    private TimerX _timerFlow;
    private TimerX _timerBatch;
    private readonly ConcurrentBag<String> _bagDay = [];
    private readonly ConcurrentBag<String> _bagHour = [];
    private readonly ConcurrentDictionary<String, ConcurrentBag<DateTime>> _bagMinute = new();
    private readonly ConcurrentQueue<TraceData> _queue = new();

    /* 延迟队列技术，周期越长则批量越大，UPDATE次数越少 */
    private readonly DayQueue _dayQueue = new() { Period = 180 };
    private readonly HourQueue _hourQueue = new() { Period = 180 };
    private readonly MinuteQueue _minuteQueue = new() { Period = 120 };
    private readonly AppMinuteQueue _appMinuteQueue = new() { Period = 120 };

    private Int32 _count;
    private readonly ITracer _tracer;

    public TraceStatService(ITracer tracer) => _tracer = tracer;

    /// <summary>添加需要统计的追踪数据</summary>
    /// <param name="traces"></param>
    public void Add(IList<TraceData> traces)
    {
        if (traces == null || traces.Count == 0) return;

        // 放入队列包，凑批处理
        foreach (var item in traces)
        {
            {
                var key = $"{item.AppId}_{item.StatDate.ToFullString()}";
                if (!_bagDay.Contains(key)) _bagDay.Add(key);
            }
            {
                var key = $"{item.AppId}_{item.StatHour.ToFullString()}";
                if (!_bagHour.Contains(key)) _bagHour.Add(key);
            }
            {
                var key = $"{item.AppId}_{item.StatMinute:yyyyMMddHH}";
                var bag = _bagMinute.GetOrAdd(key, []);
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
    /// <param name="appId"></param>
    /// <param name="time"></param>
    public void Add(Int32 appId, DateTime time)
    {
        Init();

        {
            var key = $"{appId}_{time.Date.ToFullString()}";
            if (!_bagDay.Contains(key)) _bagDay.Add(key);
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

    /// <summary>流式计算，增量累加</summary>
    /// <remarks>
    /// 性能优化：只计算分钟级统计（告警数据源），小时/天级统计由批量计算处理。
    /// 批量计算能完全修正流式计算的偏差，因此可以放心精简流式计算。
    /// </remarks>
    /// <param name="state"></param>
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

            //// 每日
            //{
            //    var st = _dayQueue.GetOrAdd(td.StatDate, td.AppId, td.ItemId, out var key);

            //    st.Total += td.Total;
            //    st.Errors += td.Errors;
            //    st.TotalCost += td.TotalCost;
            //    if (st.MaxCost < td.MaxCost) st.MaxCost = td.MaxCost;
            //    if (st.MinCost <= 0 || st.MinCost > td.MinCost && td.MinCost > 0) st.MinCost = td.MinCost;
            //    st.TotalValue += td.TotalValue;

            //    _dayQueue.Commit(key);
            //}

            //// 小时
            //{
            //    var st = _hourQueue.GetOrAdd(td.StatHour, td.AppId, td.ItemId, out var key);

            //    st.Total += td.Total;
            //    st.Errors += td.Errors;
            //    st.TotalCost += td.TotalCost;
            //    if (st.MaxCost < td.MaxCost) st.MaxCost = td.MaxCost;
            //    if (st.MinCost <= 0 || st.MinCost > td.MinCost && td.MinCost > 0) st.MinCost = td.MinCost;
            //    st.TotalValue += td.TotalValue;

            //    _hourQueue.Commit(key);
            //}

            // 分钟统计（告警数据源，需要较高实时性）
            {
                var st = _minuteQueue.GetOrAdd(td.StatMinute, td.AppId, td.ItemId, out var key);

                st.Total += td.Total;
                st.Errors += td.Errors;
                st.TotalCost += td.TotalCost;
                if (st.MaxCost < td.MaxCost) st.MaxCost = td.MaxCost;
                if (st.MinCost <= 0 || st.MinCost > td.MinCost && td.MinCost > 0) st.MinCost = td.MinCost;
                st.TotalValue += td.TotalValue;

                _minuteQueue.Commit(key);
            }

            // 应用分钟统计（告警数据源，需要较高实时性）
            {
                var st = _appMinuteQueue.GetOrAdd(td.StatMinute, td.AppId, out var key);

                st.Total += td.Total;
                st.Errors += td.Errors;
                st.TotalCost += td.TotalCost;
                if (st.MaxCost < td.MaxCost) st.MaxCost = td.MaxCost;
                if (st.MinCost <= 0 || st.MinCost > td.MinCost && td.MinCost > 0) st.MinCost = td.MinCost;

                _appMinuteQueue.Commit(key);
            }

            if (span != null) span.Value++;
        }
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

        // 聚合
        // 分组聚合，这里包含了每个接口在该日内的所有分钟统计，需要求和
        foreach (var item in list.GroupBy(e => e.ItemId))
        {
            if (item.Key == 0) continue;

            var st = _dayQueue.GetOrAdd(date, appId, item.Key, out var key);

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

            _dayQueue.Commit(key);

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

        // 分组聚合，这里包含了每个接口在该小时内的所有分钟统计，需要求和
        foreach (var item in list.GroupBy(e => e.ItemId))
        {
            if (item.Key == 0) continue;

            var st = _hourQueue.GetOrAdd(time, appId, item.Key, out var key);

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

            _hourQueue.Commit(key);

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

        // 聚合
        foreach (var item in list)
        {
            var st = _minuteQueue.GetOrAdd(item.StatMinute, appId, item.ItemId, out var key);

            st.Total = item.Total;
            st.Errors = item.Errors;
            st.TotalCost = item.TotalCost;
            st.MaxCost = item.MaxCost;
            st.MinCost = item.MinCost;
            st.TotalValue = item.TotalValue;

            _minuteQueue.Commit(key);
        }

        // 聚合应用分钟统计
        foreach (var item in list.GroupBy(e => e.AppId + "#" + e.StatMinute))
        {
            var traces = item.ToList();
            var st = _appMinuteQueue.GetOrAdd(traces[0].StatMinute, traces[0].AppId, out var key);

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

            _appMinuteQueue.Commit(key);

            if (span != null) span.Value++;
        }
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