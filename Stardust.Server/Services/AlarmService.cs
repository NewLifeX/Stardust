using System.Text;
using System.Web;
using NewLife;
using NewLife.Caching;
using NewLife.Log;
using NewLife.Threading;
using Stardust.Data;
using Stardust.Data.Monitors;
using Stardust.Data.Nodes;

namespace Stardust.Server.Services;

/// <summary>告警服务。定时检查应用性能指标、节点在线状态和Redis健康状态，触发告警通知</summary>
public class AlarmService(StarServerSetting setting, IServiceProvider serviceProvider, ITracer tracer) : IHostedService
{
    /// <summary>计算周期。默认30秒</summary>
    public Int32 Period { get; set; } = setting.AlarmPeriod;

    private TimerX _timer;
    private ICache _cache;
    private ITraceStatService _statService;

    /// <summary>启动服务，初始化定时器</summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>任务</returns>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        // 初始化定时器
        _timer = new TimerX(DoAlarm, null, 60_000, Period * 1000) { Async = true };

        return Task.CompletedTask;
    }

    /// <summary>停止服务，销毁定时器</summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>任务</returns>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _timer.TryDispose();

        return Task.CompletedTask;
    }

    /// <summary>执行告警检查。遍历应用跟踪器、节点在线状态和Redis节点，触发告警规则判断</summary>
    /// <param name="state">定时器状态参数</param>
    private void DoAlarm(Object state)
    {
        _cache ??= serviceProvider.GetService<ICacheProvider>()?.Cache;
        _statService ??= serviceProvider.GetService<ITraceStatService>();

        // 应用告警
        var list = AppTracer.FindAllWithCache();
        foreach (var item in list)
        {
            ProcessAppTracer(item);
            ProcessTraceItem(item);
            ProcessRingRate(item);
        }

        // 节点告警
        var onlines = NodeOnline.FindAll();
        foreach (var item in onlines)
        {
            if (item.Node != null) ProcessNode(item.Node);
        }

        // Redis告警
        var rnodes = RedisNode.FindAllWithCache();
        foreach (var item in rnodes)
        {
            ProcessRedisNode(item);
        }

        // 检查节点告警恢复
        CheckNodeAlarmRecovery();

        if (Period > 0) _timer.Period = Period * 1000;
    }

    #region 应用性能追踪告警
    /// <summary>处理应用性能告警。检查最近5分钟数据，判断是否超过阈值或错误率，触发通知</summary>
    /// <param name="app">应用跟踪器</param>
    private void ProcessAppTracer(AppTracer app)
    {
        // 应用是否需要告警
        if (app == null || !app.Enable) return;
        if (app.AlarmThreshold <= 0 && app.AlarmErrorRate <= 0) return;

        var appId = app.ID;
        var webhook = RobotHelper.GetAlarm(app.Project, app.Category, app.AlarmRobot);
        if (webhook.IsNullOrEmpty()) return;

        using var span = tracer?.NewSpan($"alarm:{nameof(AppTracer)}");

        // 最近一段时间的5分钟级数据。内存实时值优先（不等待落盘），数据库兜底
        var time = DateTime.Now;
        var minute = time.Date.AddHours(time.Hour).AddMinutes(time.Minute / 5 * 5);
        span?.AppendTag(new { time, minute });

        // 依次检查当前桶与上一桶，回溯上一桶用于兜底重启或恢复期间晚到落库的数据
        for (var i = 0; i <= 1; i++)
        {
            var bucket = minute.AddMinutes(-5 * i);
            var st = _statService?.GetLiveAppMinute(appId, bucket) ?? AppMinuteStat.FindByAppIdAndTime(appId, bucket);
            if (st == null) continue;

            // 内存实体尚未经过保存校验，错误率需要现场计算
            var errorRate = CalcErrorRate(st.Total, st.Errors);

            // 判断告警
            if (app.AlarmThreshold > 0 && st.Errors >= app.AlarmThreshold ||
                app.AlarmErrorRate > 0 && errorRate >= app.AlarmErrorRate)
            {
                span?.AppendTag(new { bucket, st.Errors, errorRate });

                // 一定时间内不要重复报错，除非错误翻倍
                var error2 = _cache.Get<Int32>("alarm:AppTracer:" + appId);
                if (error2 == 0 || st.Errors > error2 * 2)
                {
                    _cache.Set("alarm:AppTracer:" + appId, st.Errors, 5 * 60);
                    st.ErrorRate = errorRate;

                    var msg = GetMarkdown(app, st, true);
                    RobotHelper.SendAlarmWithRecord(app.Category ?? app.ProjectName, webhook, "应用告警", msg, "应用告警", app.Name);

                    // 触发紧急落盘，保证告警依据及时入库
                    _statService?.TriggerFlush();

                    break;
                }
            }
        }
    }

    /// <summary>生成告警消息的 Markdown 格式文本</summary>
    /// <param name="app">应用跟踪器</param>
    /// <param name="st">分钟统计数据</param>
    /// <param name="includeTitle">是否包含标题行</param>
    /// <returns>Markdown 格式的告警消息</returns>
    private String GetMarkdown(AppTracer app, AppMinuteStat st, Boolean includeTitle)
    {
        var sb = new StringBuilder();
        if (includeTitle) sb.AppendLine($"### [{app}]应用告警");
        sb.AppendLine($">**时间：**<font color=\"blue\">{st.StatTime:yyyy-MM-dd HH:mm:ss}</font>");
        sb.AppendLine($">**总数：**<font color=\"red\">{st.Errors}</font>");
        sb.AppendLine($">**错误率：**<font color=\"red\">{st.ErrorRate:p2}</font>");

        // 多地址配置（英文分号分隔）时，只取第一个（主）地址统一构造跳转链接
        var primary = GetWebUrl();
        var traceUrl = "";
        var sampleUrl = "";
        if (!primary.IsNullOrEmpty())
        {
            traceUrl = primary + "Monitors/traceMinuteStat?appId=" + st.AppId + "&minError=1";
            // 采样详情页面，用于按追踪标识查看单次调用的错误详情
            sampleUrl = primary + "Monitors/sampleData";
        }

        // 找找具体接口错误
        var names = new List<String>();
        var sts = TraceMinuteStat.FindAllByAppIdAndTime(st.AppId, st.StatTime).OrderByDescending(e => e.Errors).ToList();
        foreach (var item in sts)
        {
            if (item.Errors > 0)
            {
                sb.AppendLine($">**错误：**<font color=\"red\">埋点[{item.Name}]报错[{item.Errors:n0}]次</font>[更多]({traceUrl}&itemId={item.ItemId})");

                // 相同接口的错误，不要报多次
                if (!names.Contains(item.Name))
                {
                    var ds = TraceData.Search(st.AppId, item.ItemId, "minute", item.StatTime, 20);
                    if (ds.Count > 0)
                    {
                        // 应用节点
                        var nodes = new Dictionary<String, Node>();
                        foreach (var traceData in ds.Where(e => e.Errors > 0).OrderByDescending(e => e.Errors))
                        {
                            if (!nodes.ContainsKey(traceData.ClientId))
                            {
                                var online = AppOnline.FindByClient(traceData.ClientId);
                                var node = online?.Node;
                                if (node != null) nodes[traceData.ClientId] = node;
                            }
                        }
                        if (nodes.Count > 0) sb.AppendLine($">**节点：**<font color=\"greed\">{nodes.Select(e => e.Value.Name).Distinct().Join(",")}</font>");

                        var sms = SampleData.FindAllByDataIds(ds.Select(e => e.Id).ToArray(), item.StatTime).Where(e => !e.Error.IsNullOrEmpty()).ToList();
                        if (sms.Count > 0)
                        {
                            // 输出错误采样摘要，带上位置、实例与追踪链接
                            AppendErrorSamples(sb, sms, sampleUrl, 1);

                            names.Add(item.Name);
                        }
                        else
                        {
                            // 没有采样数据时，说明原因，避免告警只有一句错误而看不出为何没有详情
                            sb.AppendLine($">内容：无错误采样数据，请检查应用采样周期与最大错误采样数设置");

                            names.Add(item.Name);
                        }
                    }
                }
            }
        }

        var str = sb.ToString();
        if (str.Length > 1600) str = str[..1600];

        // 构造网址
        str += BuildMoreInfo($"Monitors/appMinuteStat?appId={st.AppId}&minError=1");

        return str;
    }

    /// <summary>处理跟踪项告警。检查各埋点项的分钟级数据，是否超过阈值或错误率</summary>
    /// <param name="app">应用跟踪器</param>
    private void ProcessTraceItem(AppTracer app)
    {
        if (app == null || !app.Enable) return;

        // 应用是否配置了全局跟踪项告警
        var flag = app.ItemAlarmThreshold > 0 || app.ItemAlarmErrorRate > 0;

        // 监控项单独告警
        var tis = app.TraceItems;
        if (!flag) tis = tis.Where(e => e.AlarmThreshold > 0 || e.AlarmErrorRate > 0).ToList();
        if (tis.Count > 0)
        {
            // 最近一段时间的5分钟级数据
            var time = DateTime.Now;
            var minute = time.Date.AddHours(time.Hour).AddMinutes(time.Minute / 5 * 5);

            using var span = tracer?.NewSpan($"alarm:{nameof(TraceItem)}", new { appId = app.ID, time, minute });

            var itemIds = tis.Select(e => e.Id).ToArray();
            var fired = new List<Int32>();

            // 依次检查当前桶与上一桶，回溯上一桶用于兜底重启或恢复期间晚到落库的数据
            for (var i = 0; i <= 1; i++)
            {
                var bucket = minute.AddMinutes(-5 * i);

                // 数据库行兜底，内存实时值优先覆盖（不再等待落盘）
                var list = TraceMinuteStat.Search(app.ID, bucket, itemIds);
                var dict = new Dictionary<Int32, TraceMinuteStat>();
                foreach (var st in list) dict[st.ItemId] = st;

                foreach (var ti in tis)
                {
                    // 同一个跟踪项只报一次，不跨桶重复
                    if (fired.Contains(ti.Id)) continue;

                    var max = ti.AlarmThreshold;
                    var rate = ti.AlarmErrorRate;
                    if (max <= 0 && rate <= 0)
                    {
                        max = app.ItemAlarmThreshold;
                        rate = app.ItemAlarmErrorRate;
                    }
                    if (max <= 0 && rate <= 0) continue;

                    // 内存优先：未落盘时数据库可能还没有该行
                    var live = _statService?.GetLiveMinute(app.ID, ti.Id, bucket);
                    var st = SelectLatest(live, dict.TryGetValue(ti.Id, out var db) ? db : null);
                    if (st == null) continue;

                    // 内存实体尚未经过保存校验，错误率需要现场计算
                    var errorRate = CalcErrorRate(st.Total, st.Errors);

                    // 必须两个条件同时满足，才能告警
                    if (max > 0 && st.Errors >= max &&
                        rate > 0 && errorRate >= rate)
                    {
                        span?.AppendTag(new { bucket, st.Errors, errorRate, rate });

                        // 一定时间内不要重复报错，除非错误翻倍
                        var error2 = _cache.Get<Int32>("alarm:TraceMinuteStat:" + ti.Id);
                        if (error2 == 0 || st.Errors > error2 * 2)
                        {
                            _cache.Set("alarm:TraceMinuteStat:" + ti.Id, st.Errors, 5 * 60);

                            // 优先本地跟踪项，其次应用，最后是告警分组
                            var webhook = ti.AlarmRobot;
                            if (webhook.IsNullOrEmpty()) webhook = app.AlarmRobot;

                            var group = ti.AlarmGroup;
                            if (group.IsNullOrEmpty()) group = app.Category;

                            st.ErrorRate = errorRate;

                            var msg = GetMarkdown(app, st, true);
                            RobotHelper.SendAlarmWithRecord(group, webhook, "埋点告警", msg, "埋点告警", $"{app.Name}/{ti.Name}");

                            // 触发紧急落盘，保证告警依据及时入库
                            _statService?.TriggerFlush();

                            fired.Add(ti.Id);
                        }
                    }
                }
            }
        }
    }

    /// <summary>计算错误率。内存实体尚未经过保存校验，ErrorRate 列可能滞后，需要现场计算</summary>
    /// <param name="total">总次数</param>
    /// <param name="errors">错误数</param>
    /// <returns>错误率，保留4位小数</returns>
    private static Double CalcErrorRate(Int64 total, Int64 errors) => total == 0 ? 0 : Math.Round((Double)errors / total, 4);

    /// <summary>选择较新的统计值。内存实时值优先于数据库值，避免等待落盘</summary>
    /// <param name="live">内存实时统计，可能为空</param>
    /// <param name="db">数据库统计，可能为空</param>
    /// <returns>较新的统计对象</returns>
    private static TraceMinuteStat SelectLatest(TraceMinuteStat live, TraceMinuteStat db)
    {
        if (live == null) return db;
        if (db == null || live.Errors >= db.Errors) return live;

        return db;
    }

    private String GetMarkdown(AppTracer app, TraceMinuteStat st, Boolean includeTitle)
    {
        var sb = new StringBuilder();
        if (includeTitle) sb.AppendLine($"### [{app}]埋点告警");
        sb.AppendLine($">**时间：**<font color=\"blue\">{st.StatTime:yyyy-MM-dd HH:mm:ss}</font>");
        sb.AppendLine($">**总数：**<font color=\"red\">{st.Errors}</font>");
        sb.AppendLine($">**错误率：**<font color=\"red\">{st.ErrorRate:p2}</font>");

        // 多地址配置（英文分号分隔）时，只取第一个（主）地址统一构造跳转链接
        var primary = GetWebUrl();
        var sampleUrl = "";
        if (!primary.IsNullOrEmpty())
        {
            // 采样详情页面，用于按追踪标识查看单次调用的错误详情
            sampleUrl = primary + "Monitors/sampleData";
        }

        // 找找具体接口错误
        var item = st;
        sb.AppendLine($">**错误：**<font color=\"red\">埋点[{item.Name}]报错[{item.Errors:n0}]次</font>");

        var ds = TraceData.Search(st.AppId, item.ItemId, "minute", item.StatTime, 100);
        if (ds.Count > 0)
        {
            // 应用节点
            var nodes = new Dictionary<String, Node>();
            foreach (var traceData in ds.Where(e => e.Errors > 0).OrderByDescending(e => e.Errors))
            {
                if (!nodes.ContainsKey(traceData.ClientId))
                {
                    var online = AppOnline.FindByClient(traceData.ClientId);
                    var node = online?.Node;
                    if (node != null) nodes[traceData.ClientId] = node;
                }
            }
            if (nodes.Count > 0)
            {
                var names = nodes.Select(e => e.Value.Name).Distinct().ToArray();
                sb.AppendLine($">**节点：**<font color=\"greed\">{names.Join(",")}</font>");
            }

            var sms = SampleData.FindAllByDataIds(ds.Select(e => e.Id).ToArray(), item.StatTime).Where(e => !e.Error.IsNullOrEmpty()).ToList();
            if (sms.Count > 0)
            {
                // 输出错误采样摘要，带上位置、实例与追踪链接
                AppendErrorSamples(sb, sms, sampleUrl, 3);
            }
            else
            {
                // 没有采样数据时，说明原因，避免告警只有一句错误而看不出为何没有详情
                sb.AppendLine($">内容：无错误采样数据，请检查应用采样周期与最大错误采样数设置");
            }
        }

        var str = sb.ToString();
        if (str.Length > 1600) str = str[..1600];

        // 构造网址
        str += BuildMoreInfo($"Monitors/traceData?appId={st.AppId}&kind=minute&time={HttpUtility.UrlEncode(st.StatTime.ToFullString())}&itemId={st.ItemId}&minError=1");

        return str;
    }

    /// <summary>输出错误采样摘要。除异常首行外，附带抛出位置、实例与追踪链接，便于定位问题</summary>
    /// <param name="sb">消息构造器</param>
    /// <param name="samples">错误采样数据</param>
    /// <param name="sampleUrl">采样详情页面地址，为空时不输出追踪链接</param>
    /// <param name="maxCount">最多输出的采样条数</param>
    private void AppendErrorSamples(StringBuilder sb, IList<SampleData> samples, String sampleUrl, Int32 maxCount)
    {
        if (samples == null || samples.Count == 0) return;

        var count = 0;
        var keys = new List<String>();
        foreach (var sm in samples)
        {
            if (count >= maxCount) break;

            var error = sm.Error?.Trim();
            if (error.IsNullOrEmpty()) continue;

            // 异常首行，即异常类型加消息
            var msg = GetFirstLine(error);
            if (msg.IsNullOrEmpty()) continue;

            // 同一异常的多次采样只报一次，避免刷屏
            if (keys.Contains(msg)) continue;
            keys.Add(msg);

            sb.AppendLine($">内容：{msg}");

            // 堆栈中第一个非框架帧，用于定位抛出位置
            var frame = GetStackFrame(error);
            if (!frame.IsNullOrEmpty()) sb.AppendLine($">位置：`{frame}`");

            // 发生时间与耗时。耗时接近阻塞时长时，多为等待被取消，而非真正的执行失败
            sb.AppendLine($">时间：{sm.Start:HH:mm:ss}，耗时：{sm.Cost:n0}ms");

            // 实例即ip@进程号，同名节点的多个实例可据此区分
            if (!sm.ClientId.IsNullOrEmpty())
            {
                if (!sampleUrl.IsNullOrEmpty() && !sm.TraceId.IsNullOrEmpty())
                    sb.AppendLine($">实例：{sm.ClientId}，[追踪]({sampleUrl}?traceId={HttpUtility.UrlEncode(sm.TraceId)})");
                else
                    sb.AppendLine($">实例：{sm.ClientId}");
            }

            count++;
        }
    }

    /// <summary>取得多行文本的第一行，超长时截断</summary>
    /// <param name="value">多行文本</param>
    private static String GetFirstLine(String value)
    {
        if (value.IsNullOrEmpty()) return null;

        var p = value.IndexOfAny(['\r', '\n']);
        var line = p > 0 ? value[..p] : value;

        return Cut(line.Trim(), 160);
    }

    /// <summary>取得堆栈中第一个非框架调用帧，用于定位业务代码位置</summary>
    /// <param name="value">异常信息，含堆栈</param>
    private static String GetStackFrame(String value)
    {
        if (value.IsNullOrEmpty()) return null;

        var lines = value.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        for (var i = 1; i < lines.Length && i < 16; i++)
        {
            var line = lines[i].Trim();
            if (!line.StartsWith("at ")) continue;

            var frame = line[3..].Trim();
            if (frame.IsNullOrEmpty()) continue;

            // 跳过框架内部帧，尽量定位到业务代码
            if (frame.StartsWith("System.") || frame.StartsWith("Microsoft.") || frame.StartsWith("NewLife.")) continue;

            return Cut(frame, 160);
        }

        return null;
    }

    /// <summary>截断字符串，避免单条内容过长挤占整个告警消息</summary>
    /// <param name="value">原字符串</param>
    /// <param name="maxLength">最大长度</param>
    private static String Cut(String value, Int32 maxLength)
    {
        if (value.IsNullOrEmpty() || value.Length <= maxLength) return value;

        return value[..maxLength] + "...";
    }

    /// <summary>取得星尘管理平台基础地址。配置支持英文分号分隔多个地址（多实例部署），统一在此解析与规范化，并只取第一个（主）地址</summary>
    /// <returns>规范化后的主地址，以/结尾；未配置时返回空字符串</returns>
    private String GetWebUrl()
    {
        var webUrl = setting.WebUrl;
        if (webUrl.IsNullOrEmpty()) return "";

        // 按英文分号拆分多个地址，去除首尾空白，并统一补全结尾的/
        var first = webUrl.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();
        return first.IsNullOrEmpty() ? "" : first.EnsureEnd("/");
    }

    /// <summary>构造“更多信息”跳转链接文本。多地址配置时只使用第一个（主）地址</summary>
    /// <param name="relativeUrl">相对路径（不含基础地址，且不带前导/）</param>
    /// <returns>链接文本；无配置时返回空字符串</returns>
    private String BuildMoreInfo(String relativeUrl)
    {
        var baseUrl = GetWebUrl();
        if (baseUrl.IsNullOrEmpty()) return "";

        return Environment.NewLine + $"[更多信息]({baseUrl}{relativeUrl})";
    }

    /// <summary>处理环比告警。对比今日与昨日相同时段的错误率变化趋势</summary>
    /// <param name="app">应用跟踪器</param>
    private void ProcessRingRate(AppTracer app)
    {
        if (app == null || !app.Enable) return;

        // 监控项单独告警
        var tis = app.TraceItems.Where(e => e.MaxRingRate > 0 || e.MinRingRate > 0).ToList();
        if (tis.Count <= 0) return;

        // 最近一段时间的小时级数据
        var time = DateTime.Now;
        var hour = time.Date.AddHours(time.Hour);
        if (time.Minute < 5) return;

        using var span = tracer?.NewSpan($"alarm:RingRate", new { app.ID, app.Name, app.DisplayName, time, hour });

        var list = TraceHourStat.Search(app.ID, -1, null, hour, hour.AddHours(1), null, null);
        foreach (var st in list)
        {
            var ti = tis.FirstOrDefault(e => e.Id == st.ItemId);
            if (ti != null && st.RingRate > 0)
            {
                var max = ti.MaxRingRate;
                var min = ti.MinRingRate;

                // 昨日基数必须大于一定值，避免分母过小导致误报
                var st2 = TraceHourStat.FindAllByStatTimeAndAppIdAndItemId(st.StatTime.AddDays(-1), st.AppId, st.ItemId).FirstOrDefault();
                var yesterday = st2 != null ? st2.Total : (st.Total / st.RingRate);
                if (yesterday > 10)
                {
                    // 根据当前小时已过去时间，折算得到新的环比率
                    var seconds = time.Minute * 60 + time.Second;
                    var rate = st.Total / (yesterday * seconds / 3600);

                    // 满足任意一个条件，都要告警
                    if (max > 0 && rate >= max ||
                        min > 0 && rate <= min)
                    {
                        span?.AppendTag(new { seconds, yesterday, rate });

                        // 一定时间内不要重复报错，除非错误翻倍
                        var error2 = _cache.Get<Double>("alarm:RingRate:" + ti.Id);
                        if (error2 == 0 || rate > error2 * 2 || rate < error2 / 2)
                        {
                            _cache.Set("alarm:RingRate:" + ti.Id, rate, 60 * 60);

                            // 优先本地跟踪项，其次应用，最后是告警分组
                            var webhook = ti.AlarmRobot;
                            if (webhook.IsNullOrEmpty()) webhook = app.AlarmRobot;

                            var group = ti.AlarmGroup;
                            if (group.IsNullOrEmpty()) group = app.Category;

                            var msg = GetMarkdown(app, st, (Int32)yesterday, rate, true);
                            RobotHelper.SendAlarmWithRecord(group, webhook, "埋点告警", msg, "环比告警", $"{app.Name}/{ti.Name}");
                        }
                    }
                }
            }
        }
    }

    private String GetMarkdown(AppTracer app, TraceHourStat st, Int32 yesterday, Double rate, Boolean includeTitle)
    {
        var sb = new StringBuilder();
        if (includeTitle) sb.AppendLine($"### [{app}]环比{(rate >= 1 ? "高调用" : "调用量下滑")}告警");
        sb.AppendLine($">**埋点：**<font color=\"blue\">{st.Name}</font>");
        sb.AppendLine($">**时间：**<font color=\"blue\">{st.StatTime:yyyy-MM-dd HH:mm:ss}</font>");
        sb.AppendLine($">**今日：**<font color=\"red\">{st.Total}</font>");
        sb.AppendLine($">**昨日：**<font color=\"red\">{yesterday}</font>");
        sb.AppendLine($">**环比：**<font color=\"red\">{st.RingRate:p2}</font>");
        sb.AppendLine($">**折算环比：**<font color=\"red\">{rate:p2}</font>");

        var str = sb.ToString();
        if (str.Length > 1600) str = str[..1600];

        // 构造网址
        str += BuildMoreInfo($"Monitors/traceHourStat?appId={st.AppId}&itemId={st.ItemId}");

        return str;
    }
    #endregion

    #region 节点告警
    /// <summary>处理节点告警。检查节点的 CPU/内存/磁盘/温度/进程数/网络等指标是否超过阈值</summary>
    /// <param name="node">节点</param>
    private void ProcessNode(Node node)
    {
        if (node == null || !node.Enable) return;

        var webhook = RobotHelper.GetAlarm(node.Project, node.Category, node.WebHook);
        if (webhook.IsNullOrEmpty()) return;

        if (node.AlarmCpuRate <= 0 && node.AlarmMemoryRate <= 0 && node.AlarmDiskRate <= 0 && node.AlarmProcesses.IsNullOrEmpty()) return;

        using var span = tracer?.NewSpan($"alarm:{nameof(Node)}");

        // 最新数据
        var data = NodeData.FindLast(node.ID);
        if (data == null) return;

        // CPU告警
        if (node.AlarmCpuRate > 0)
        {
            var rate = data.CpuRate * 100;
            if (rate >= node.AlarmCpuRate)
            {
                // 一定时间内不要重复报错，除非错误翻倍
                var error2 = _cache.Get<Double>("alarm:CpuRate:" + node.ID);
                if (error2 == 0 || rate > error2 * 2)
                {
                    _cache.Set("alarm:CpuRate:" + node.ID, rate, 5 * 60);

                    SendAlarm("cpu", node, data, $"[{node.Name}]CPU告警");
                }
            }
        }

        // 内存告警
        if (node.AlarmMemoryRate > 0 && node.Memory > 0)
        {
            var free = data.FreeMemory > 0 ? data.FreeMemory : data.AvailableMemory;
            var rate = (node.Memory - free) * 100d / node.Memory;
            if (rate >= node.AlarmMemoryRate)
            {
                // 一定时间内不要重复报错，除非错误翻倍
                var error2 = _cache.Get<Double>("alarm:MemoryRate:" + node.ID);
                if (error2 == 0 || rate > error2 * 2)
                {
                    _cache.Set("alarm:MemoryRate:" + node.ID, rate, 5 * 60);

                    SendAlarm("memory", node, data, $"[{node.Name}]内存告警");
                }
            }
        }

        // 磁盘告警
        if (node.AlarmDiskRate > 0 && node.TotalSize > 0)
        {
            var rate = (node.TotalSize - data.AvailableFreeSpace) * 100d / node.TotalSize;
            if (rate >= node.AlarmDiskRate)
            {
                // 一定时间内不要重复报错，除非错误翻倍
                var error2 = _cache.Get<Double>("alarm:DiskRate:" + node.ID);
                if (error2 == 0 || rate > error2 * 2)
                {
                    _cache.Set("alarm:DiskRate:" + node.ID, rate, 5 * 60);

                    SendAlarm("disk", node, data, $"[{node.Name}]磁盘告警");
                }
            }
        }

        // TCP告警
        if (node.AlarmTcp > 0)
        {
            var tcp = data.TcpConnections;
            if (tcp < data.TcpTimeWait) tcp = data.TcpTimeWait;
            if (tcp < data.TcpCloseWait) tcp = data.TcpCloseWait;
            if (tcp >= node.AlarmTcp)
            {
                // 一定时间内不要重复报错，除非错误翻倍
                var error2 = _cache.Get<Int32>("alarm:Tcp:" + node.ID);
                if (error2 == 0 || tcp > error2 * 2)
                {
                    _cache.Set("alarm:Tcp:" + node.ID, tcp, 5 * 60);

                    SendAlarm("tcp", node, data, $"[{node.Name}]Tcp告警");
                }
            }
        }

        // 进程告警
        if (!node.AlarmProcesses.IsNullOrEmpty())
        {
            var olt = NodeOnline.FindByNodeId(node.ID);
            if (olt != null && !olt.Processes.IsNullOrEmpty())
            {
                var alarms = node.AlarmProcesses.Split(",", StringSplitOptions.RemoveEmptyEntries);
                var ps = olt.Processes?.Split(",", StringSplitOptions.RemoveEmptyEntries);
                if (alarms != null && alarms.Length > 0 && ps != null && ps.Length > 0)
                {
                    // 查找丢失的进程
                    var ps2 = alarms.Where(e => !ps.Contains(e)).ToList();
                    if (ps2.Count > 0)
                    {
                        // 一定时间内不要重复报错
                        var error2 = _cache.Get<Int32>("alarm:Process:" + node.ID);
                        if (error2 == 0 || ps2.Count > error2)
                        {
                            _cache.Set("alarm:Process:" + node.ID, ps2.Count, 5 * 60);

                            SendAlarm("process", node, data, $"[{node.Name}]进程守护告警", ps2.Join());
                        }
                    }
                }
            }
        }
    }

    private void SendAlarm(String kind, Node node, NodeData data, String title, String info = null)
    {
        var msg = GetMarkdown(kind, node, data, title, info);
        RobotHelper.SendAlarmWithRecord(node.Category, node.WebHook, title, msg, $"节点{kind}告警", node.Name);
    }

    private String GetMarkdown(String kind, Node node, NodeData data, String title, String msg = null)
    {
        var sb = new StringBuilder();
        if (!title.IsNullOrEmpty()) sb.AppendLine($"### {title}");
        sb.AppendLine($">**时间：**<font color=\"blue\">{data.CreateTime:yyyy-MM-dd HH:mm:ss}</font>");
        sb.AppendLine($">**节点：**<font color=\"gray\">{node} / {node.IP}</font>");
        sb.AppendLine($">**分类：**<font color=\"gray\">{node.Category}</font>");
        sb.AppendLine($">**系统：**<font color=\"gray\">{node.OS}</font>");
        sb.AppendLine($">**CPU核心：**<font color=\"gray\">{node.Cpu}</font>");
        sb.AppendLine($">**内存容量：**<font color=\"gray\">{node.Memory:n0}M，可用 {data.AvailableMemory:n0}M，空闲 {data.FreeMemory:n0}M</font>");
        sb.AppendLine($">**磁盘容量：**<font color=\"gray\">{node.TotalSize:n0}M，可用 {data.AvailableFreeSpace:n0}M</font>");

        switch (kind)
        {
            case "cpu":
                sb.AppendLine($">**CPU使用率：**<font color=\"red\">{data.CpuRate:p0} >= {node.AlarmCpuRate / 100d:p0}</font>");
                break;
            case "memory":
                var free = data.FreeMemory > 0 ? data.FreeMemory : data.AvailableMemory;
                var rate1 = 1 - (node.Memory == 0 ? 0 : ((Double)free / node.Memory));
                sb.AppendLine($">**内存使用率：**<font color=\"red\">{rate1:p0} >= {node.AlarmMemoryRate / 100d:p0}</font>");
                break;
            case "disk":
                var rate2 = 1 - (node.TotalSize == 0 ? 0 : ((Double)data.AvailableFreeSpace / node.TotalSize));
                sb.AppendLine($">**磁盘使用率：**<font color=\"red\"> {rate2:p0} >= {node.AlarmDiskRate / 100d:p0}</font>");
                break;
            case "tcp":
                if (data.TcpConnections >= node.AlarmTcp)
                    sb.AppendLine($">**TCP连接数：**<font color=\"red\">{data.TcpConnections:n0} >= {node.AlarmTcp:n0}</font>");
                if (data.TcpTimeWait >= node.AlarmTcp)
                    sb.AppendLine($">**TCP主动关闭：**<font color=\"red\">{data.TcpTimeWait:n0} >= {node.AlarmTcp:n0}</font>");
                if (data.TcpCloseWait >= node.AlarmTcp)
                    sb.AppendLine($">**TCP被动关闭：**<font color=\"red\">{data.TcpCloseWait:n0} >= {node.AlarmTcp:n0}</font>");
                break;
            case "process":
                sb.AppendLine($">**进程已退出：**<font color=\"red\">{msg}</font>");
                break;
        }

        var str = sb.ToString();
        if (str.Length > 2000) str = str[..2000];

        // 构造网址
        str += BuildMoreInfo($"Nodes/NodeData?nodeId={node.ID}");

        return str;
    }
    #endregion

    #region Redis告警
    /// <summary>处理 Redis 节点告警。检查 Redis 节点和消息队列的健康状态</summary>
    /// <param name="node">Redis 节点</param>
    private void ProcessRedisNode(RedisNode node)
    {
        if (node == null || !node.Enable) return;

        ProcessRedisData(node);
        ProcessRedisQueue(node);
    }

    private void ProcessRedisData(RedisNode node)
    {
        //if (!RobotHelper.CanAlarm(node.Category, node.WebHook)) return;
        if (node.AlarmMemoryRate <= 0 || node.AlarmConnections == 0) return;

        var webhook = RobotHelper.GetAlarm(node.Project, node.Category, node.WebHook);
        if (webhook.IsNullOrEmpty()) return;

        // 最新数据
        var data = RedisData.FindLast(node.Id);
        if (data == null) return;

        using var span = tracer?.NewSpan($"alarm:{nameof(RedisNode)}");

        var actions = new List<Action<StringBuilder>>();

        // 内存告警
        var rate = data.UsedMemory * 100d / node.MaxMemory;
        if (rate >= node.AlarmMemoryRate)
        {
            // 一定时间内不要重复报错，除非错误翻倍
            var error2 = _cache.Get<Double>("alarm:RedisMemory:" + node.Id);
            if (error2 == 0 || rate > error2 * 2)
            {
                _cache.Set("alarm:RedisMemory:" + node.Id, rate, 5 * 60);

                actions.Add(sb => sb.AppendLine($">**内存告警：**<font color=\"red\">{rate / 100:p0} >= {node.AlarmMemoryRate / 100:p0}</font>"));
            }
        }

        // 连接数告警
        var cs = data.ConnectedClients;
        if (node.AlarmConnections > 0 && cs >= node.AlarmConnections)
        {
            // 一定时间内不要重复报错，除非错误翻倍
            var error2 = _cache.Get<Int32>("alarm:RedisConnections:" + node.Id);
            if (error2 == 0 || cs > error2 * 2)
            {
                _cache.Set("alarm:RedisConnections:" + node.Id, cs, 5 * 60);

                actions.Add(sb => sb.AppendLine($">**连接数告警：**<font color=\"red\">{cs:n0} >= {node.AlarmConnections:n0}</font>"));
            }
        }

        // 速度告警
        var speed = data.Speed;
        if (node.AlarmSpeed > 0 && speed >= node.AlarmSpeed)
        {
            // 一定时间内不要重复报错，除非错误翻倍
            var error2 = _cache.Get<Int32>("alarm:RedisSpeed:" + node.Id);
            if (error2 == 0 || speed > error2 * 2)
            {
                _cache.Set("alarm:RedisSpeed:" + node.Id, speed, 5 * 60);

                actions.Add(sb => sb.AppendLine($">**速度告警：**<font color=\"red\">{speed:n0} >= {node.AlarmSpeed:n0}</font>"));
            }
        }

        // 入流量告警
        var input = data.InputKbps;
        if (node.AlarmInputKbps > 0 && input >= node.AlarmInputKbps)
        {
            // 一定时间内不要重复报错，除非错误翻倍
            var error2 = _cache.Get<Double>("alarm:RedisInputKbps:" + node.Id);
            if (error2 == 0 || input > error2 * 2)
            {
                _cache.Set("alarm:RedisInputKbps:" + node.Id, input, 5 * 60);

                actions.Add(sb => sb.AppendLine($">**入流量告警：**<font color=\"red\">{input:n0} >= {node.AlarmInputKbps:n0}</font>"));
            }
        }

        // 出流量告警
        var output = data.OutputKbps;
        if (node.AlarmOutputKbps > 0 && output >= node.AlarmOutputKbps)
        {
            // 一定时间内不要重复报错，除非错误翻倍
            var error2 = _cache.Get<Double>("alarm:RedisOutputKbps:" + node.Id);
            if (error2 == 0 || output > error2 * 2)
            {
                _cache.Set("alarm:RedisOutputKbps:" + node.Id, output, 5 * 60);

                actions.Add(sb => sb.AppendLine($">**出流量告警：**<font color=\"red\">{output:n0} >= {node.AlarmOutputKbps:n0}</font>"));
            }
        }

        if (actions.Count > 0)
        {
            var msg = GetMarkdown(node, data, "Redis告警", actions);
            RobotHelper.SendAlarmWithRecord(node.Category, node.WebHook, "Redis告警", msg, "Redis告警", node.Name);
        }
    }

    private String GetMarkdown(RedisNode node, RedisData data, String title, IList<Action<StringBuilder>> actions)
    {
        var sb = new StringBuilder();
        if (!title.IsNullOrEmpty()) sb.AppendLine($"### [{node}]{title}");
        sb.AppendLine($">**时间：**<font color=\"blue\">{data.CreateTime:yyyy-MM-dd HH:mm:ss}</font>");
        sb.AppendLine($">**分类：**<font color=\"gray\">{node.Category}</font>");
        sb.AppendLine($">**版本：**<font color=\"gray\">{node.Version}</font>");
        sb.AppendLine($">**已用内存：**<font color=\"gray\">{data.UsedMemory:n0}</font>");
        sb.AppendLine($">**内存容量：**<font color=\"gray\">{node.MaxMemory:n0}</font>");
        sb.AppendLine($">**连接数：**<font color=\"gray\">{data.ConnectedClients:n0}</font>");
        sb.AppendLine($">**服务器：**<font color=\"gray\">{node.Server}</font>");

        //var rate = node.MaxMemory == 0 ? 0 : (data.UsedMemory * 100 / node.MaxMemory);
        //if (rate >= node.AlarmMemoryRate && node.AlarmMemoryRate > 0)
        //{
        //    sb.AppendLine($">**内存告警：**<font color=\"info\">{data.UsedMemory}/{node.MaxMemory} >= {node.AlarmMemoryRate:p0}</font>");
        //}

        //if (node.AlarmConnections > 0 && data.ConnectedClients >= node.AlarmConnections)
        //{
        //    sb.AppendLine($">**连接告警：**<font color=\"info\">{data.ConnectedClients:n0} >= {node.AlarmConnections:n0}</font>");
        //}
        foreach (var item in actions)
        {
            item(sb);
        }

        var str = sb.ToString();
        if (str.Length > 2000) str = str[..2000];

        // 构造网址
        str += BuildMoreInfo($"Nodes/RedisNode?id=" + node.Id);

        return str;
    }
    #endregion

    #region Redis队列告警
    private void ProcessRedisQueue(RedisNode node)
    {
        using var span = tracer?.NewSpan($"alarm:{nameof(RedisMessageQueue)}");

        // 所有队列
        var list = RedisMessageQueue.FindAllByRedisId(node.Id);
        foreach (var queue in list)
        {
            var groupName = !queue.Category.IsNullOrEmpty() ? queue.Category : node.Category;
            var webhook = !queue.WebHook.IsNullOrEmpty() ? queue.WebHook : node.WebHook;

            // 判断告警
            if (queue.Enable && queue.MaxMessages > 0 && queue.Messages >= queue.MaxMessages)
            {
                webhook = RobotHelper.GetAlarm(node.Project, groupName, webhook);
                if (webhook.IsNullOrEmpty()) continue;

                // 一定时间内不要重复报错，除非错误翻倍
                var error2 = _cache.Get<Int32>("alarm:RedisMessageQueue:" + queue.Id);
                if (error2 == 0 || queue.Messages > error2 * 2)
                {
                    _cache.Set("alarm:RedisMessageQueue:" + queue.Id, queue.Messages, 5 * 60);

                    var msg = GetMarkdown(node, queue, true);
                    RobotHelper.SendAlarmWithRecord(groupName, webhook, "消息队列告警", msg, "消息队列告警", $"{node.Name}/{queue.Name}");
                }
            }
        }
    }

    private String GetMarkdown(RedisNode node, RedisMessageQueue queue, Boolean includeTitle)
    {
        var sb = new StringBuilder();
        if (includeTitle) sb.AppendLine($"### [{queue.Name}/{node}]消息队列告警");
        sb.AppendLine($">**时间：**<font color=\"blue\">{queue.UpdateTime:yyyy-MM-dd HH:mm:ss}</font>");
        sb.AppendLine($">**主题：**<font color=\"gray\">{queue.Topic}</font>");
        sb.AppendLine($">**积压：**<font color=\"red\">{queue.Messages:n0} > {queue.MaxMessages:n0}</font>");
        sb.AppendLine($">**消费者：**<font color=\"green\">{queue.Consumers}</font>");
        sb.AppendLine($">**总消费：**<font color=\"green\">{queue.Total:n0}</font>");
        sb.AppendLine($">**服务器：**<font color=\"gray\">{node.Server}</font>");

        var str = sb.ToString();
        if (str.Length > 2000) str = str[..2000];

        // 构造网址
        str += BuildMoreInfo($"Nodes/RedisMessageQueue?redisId=" + queue.RedisId + "&q=" + queue.Name);

        return str;
    }
    #endregion

    #region 告警恢复
    /// <summary>检查节点告警恢复。将已恢复正常状态的节点告警标记为已恢复</summary>
    private void CheckNodeAlarmRecovery()
    {
        using var span = tracer?.NewSpan("alarm:CheckRecovery");

        // 获取所有报警中的记录
        var records = AlarmRecord.FindAllByStatus(AlarmStatuses.Alarming);
        if (records.Count == 0) return;

        foreach (var record in records)
        {
            // 超过24小时的报警记录自动恢复
            if (record.StartTime.Year > 2000 && (DateTime.Now - record.StartTime).TotalHours > 24)
            {
                record.Status = AlarmStatuses.Recovered;
                record.EndTime = DateTime.Now;
                record.Duration = (Int32)(record.EndTime - record.StartTime).TotalSeconds;
                record.Update();
                continue;
            }

            // 根据类别检查是否可以恢复
            switch (record.Category)
            {
                case "节点cpu告警":
                case "节点memory告警":
                case "节点disk告警":
                case "节点tcp告警":
                case "节点process告警":
                    CheckNodeRecovery(record);
                    break;
            }
        }
    }

    private void CheckNodeRecovery(AlarmRecord record)
    {
        var node = Node.FindByName(record.Action);
        if (node == null) return;

        var data = NodeData.FindLast(node.ID);
        if (data == null) return;

        var recovered = false;
        switch (record.Category)
        {
            case "节点cpu告警":
                if (node.AlarmCpuRate <= 0 || data.CpuRate * 100 < node.AlarmCpuRate)
                    recovered = true;
                break;
            case "节点memory告警":
                if (node.AlarmMemoryRate <= 0 || node.Memory <= 0)
                    recovered = true;
                else
                {
                    var free = data.FreeMemory > 0 ? data.FreeMemory : data.AvailableMemory;
                    var rate = (node.Memory - free) * 100d / node.Memory;
                    if (rate < node.AlarmMemoryRate) recovered = true;
                }
                break;
            case "节点disk告警":
                if (node.AlarmDiskRate <= 0 || node.TotalSize <= 0)
                    recovered = true;
                else
                {
                    var rate = (node.TotalSize - data.AvailableFreeSpace) * 100d / node.TotalSize;
                    if (rate < node.AlarmDiskRate) recovered = true;
                }
                break;
            case "节点tcp告警":
                if (node.AlarmTcp <= 0)
                    recovered = true;
                else
                {
                    var tcp = data.TcpConnections;
                    if (tcp < data.TcpTimeWait) tcp = data.TcpTimeWait;
                    if (tcp < data.TcpCloseWait) tcp = data.TcpCloseWait;
                    if (tcp < node.AlarmTcp) recovered = true;
                }
                break;
            case "节点process告警":
                var olt = NodeOnline.FindByNodeId(node.ID);
                if (olt != null && !olt.Processes.IsNullOrEmpty() && !node.AlarmProcesses.IsNullOrEmpty())
                {
                    var alarms = node.AlarmProcesses.Split(",", StringSplitOptions.RemoveEmptyEntries);
                    var ps = olt.Processes?.Split(",", StringSplitOptions.RemoveEmptyEntries);
                    if (alarms != null && ps != null)
                    {
                        var ps2 = alarms.Where(e => !ps.Contains(e)).ToList();
                        if (ps2.Count == 0) recovered = true;
                    }
                }
                break;
        }

        if (recovered)
        {
            var webhook = RobotHelper.GetAlarm(node.Project, node.Category, node.WebHook);
            if (!webhook.IsNullOrEmpty())
                RobotHelper.RecoverAlarm(record.Category, record.Action, node.Category, webhook);
        }
    }
    #endregion
}