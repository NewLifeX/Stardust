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

public class AlarmService(StarServerSetting setting, IServiceProvider serviceProvider, ITracer tracer) : IHostedService
{
    /// <summary>计算周期。默认30秒</summary>
    public Int32 Period { get; set; } = setting.AlarmPeriod;

    private TimerX _timer;
    private ICache _cache;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        // 初始化定时器
        _timer = new TimerX(DoAlarm, null, 60_000, Period * 1000) { Async = true };

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _timer.TryDispose();

        return Task.CompletedTask;
    }

    private void DoAlarm(Object state)
    {
        _cache ??= serviceProvider.GetService<ICacheProvider>()?.Cache;

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

        if (Period > 0) _timer.Period = Period * 1000;
    }

    #region 应用性能追踪告警
    private void ProcessAppTracer(AppTracer app)
    {
        // 应用是否需要告警
        if (app == null || !app.Enable) return;
        if (app.AlarmThreshold <= 0 && app.AlarmErrorRate <= 0) return;

        var appId = app.ID;
        var webhook = RobotHelper.GetAlarm(app.Project, app.Category, app.AlarmRobot);
        if (webhook.IsNullOrEmpty()) return;

        using var span = tracer?.NewSpan($"alarm:{nameof(AppTracer)}");

        // 最近一段时间的5分钟级数据
        var time = DateTime.Now;
        var minute = time.Date.AddHours(time.Hour).AddMinutes(time.Minute / 5 * 5);
        span?.AppendTag(new { time, minute });

        var st = AppMinuteStat.FindByAppIdAndTime(appId, minute);
        if (st != null)
        {
            // 判断告警
            if (app.AlarmThreshold > 0 && st.Errors >= app.AlarmThreshold ||
                app.AlarmErrorRate > 0 && st.ErrorRate >= app.AlarmErrorRate)
            {
                span?.AppendTag(new { st.Errors, st.ErrorRate });

                // 一定时间内不要重复报错，除非错误翻倍
                var error2 = _cache.Get<Int32>("alarm:AppTracer:" + appId);
                if (error2 == 0 || st.Errors > error2 * 2)
                {
                    _cache.Set("alarm:AppTracer:" + appId, st.Errors, 5 * 60);

                    var msg = GetMarkdown(app, st, true);
                    RobotHelper.SendAlarm(app.Category ?? app.ProjectName, webhook, "应用告警", msg);
                }
            }
        }
    }

    private String GetMarkdown(AppTracer app, AppMinuteStat st, Boolean includeTitle)
    {
        var sb = new StringBuilder();
        if (includeTitle) sb.AppendLine($"### [{app}]应用告警");
        sb.AppendLine($">**时间：**<font color=\"blue\">{st.StatTime:yyyy-MM-dd HH:mm:ss}</font>");
        sb.AppendLine($">**总数：**<font color=\"red\">{st.Errors}</font>");
        sb.AppendLine($">**错误率：**<font color=\"red\">{st.ErrorRate:p2}</font>");

        var url = setting.WebUrl;
        var appUrl = "";
        var traceUrl = "";
        if (!url.IsNullOrEmpty())
        {
            appUrl = url.EnsureEnd("/") + "Monitors/appMinuteStat?appId=" + st.AppId + "&minError=1";
            traceUrl = url.EnsureEnd("/") + "Monitors/traceMinuteStat?appId=" + st.AppId + "&minError=1";
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
                        if (nodes.Count > 0) sb.AppendLine($">**节点：**<font color=\"greed\">{nodes.Join(",", e => e.Value.Name)}</font>");

                        var sms = SampleData.FindAllByDataIds(ds.Select(e => e.Id).ToArray(), item.StatTime).Where(e => !e.Error.IsNullOrEmpty()).ToList();
                        if (sms.Count > 0)
                        {
                            var msg = sms[0].Error?.Trim();
                            if (!msg.IsNullOrEmpty())
                            {
                                // 错误内容取第一行，详情看更多
                                var p = msg.IndexOfAny(new[] { '\r', '\n' });
                                if (p > 0) msg = msg[..p];

                                sb.AppendLine($">内容：{msg}");

                                names.Add(item.Name);
                            }
                        }
                    }
                }
            }
        }

        var str = sb.ToString();
        if (str.Length > 1600) str = str[..1600];

        // 构造网址
        if (!appUrl.IsNullOrEmpty())
        {
            str += Environment.NewLine + $"[更多信息]({appUrl})";
        }

        return str;
    }

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

            var list = TraceMinuteStat.Search(app.ID, minute, tis.Select(e => e.Id).ToArray());
            foreach (var st in list)
            {
                var ti = tis.FirstOrDefault(e => e.Id == st.ItemId);
                if (ti != null)
                {
                    var max = ti.AlarmThreshold;
                    var rate = ti.AlarmErrorRate;
                    if (max <= 0 && rate <= 0)
                    {
                        max = app.ItemAlarmThreshold;
                        rate = app.ItemAlarmErrorRate;
                    }

                    // 必须两个条件同时满足，才能告警
                    if (max > 0 && st.Errors >= max &&
                        rate > 0 && st.ErrorRate >= rate)
                    {
                        span?.AppendTag(new { st.Errors, st.ErrorRate, rate });

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

                            var msg = GetMarkdown(app, st, true);
                            RobotHelper.SendAlarm(group, webhook, "埋点告警", msg);
                        }
                    }
                }
            }
        }
    }

    private String GetMarkdown(AppTracer app, TraceMinuteStat st, Boolean includeTitle)
    {
        var sb = new StringBuilder();
        if (includeTitle) sb.AppendLine($"### [{app}]埋点告警");
        sb.AppendLine($">**时间：**<font color=\"blue\">{st.StatTime:yyyy-MM-dd HH:mm:ss}</font>");
        sb.AppendLine($">**总数：**<font color=\"red\">{st.Errors}</font>");
        sb.AppendLine($">**错误率：**<font color=\"red\">{st.ErrorRate:p2}</font>");

        var url = setting.WebUrl;
        var traceUrl = "";
        if (!url.IsNullOrEmpty())
        {
            traceUrl = url.EnsureEnd("/") + $"Monitors/traceData?appId={st.AppId}&kind=minute&time={HttpUtility.UrlEncode(st.StatTime.ToFullString())}&itemId={st.ItemId}&minError=1";
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
                var msg = sms[0].Error?.Trim();
                if (!msg.IsNullOrEmpty())
                {
                    // 错误内容取第一行，详情看更多
                    var p = msg.IndexOfAny(new[] { '\r', '\n' });
                    if (p > 0) msg = msg[..p];

                    sb.AppendLine($">内容：{msg}");
                }
            }
        }

        var str = sb.ToString();
        if (str.Length > 1600) str = str[..1600];

        // 构造网址
        if (!traceUrl.IsNullOrEmpty())
        {
            str += Environment.NewLine + $"[更多信息]({traceUrl})";
        }

        return str;
    }

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
                            RobotHelper.SendAlarm(group, webhook, "埋点告警", msg);
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

        var url = setting.WebUrl;
        var traceUrl = "";
        if (!url.IsNullOrEmpty())
        {
            traceUrl = url.EnsureEnd("/") + $"Monitors/traceHourStat?appId={st.AppId}&itemId={st.ItemId}";
        }

        var str = sb.ToString();
        if (str.Length > 1600) str = str[..1600];

        // 构造网址
        if (!traceUrl.IsNullOrEmpty())
        {
            str += Environment.NewLine + $"[更多信息]({traceUrl})";
        }

        return str;
    }
    #endregion

    #region 节点告警
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
            var rate = (node.Memory - data.AvailableMemory) * 100d / node.Memory;
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
        RobotHelper.SendAlarm(node.Category, node.WebHook, title, msg);
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
        sb.AppendLine($">**内存容量：**<font color=\"gray\">{node.Memory:n0}M，可用 {data.AvailableMemory:n0}M</font>");
        sb.AppendLine($">**磁盘容量：**<font color=\"gray\">{node.TotalSize:n0}M，可用 {data.AvailableFreeSpace:n0}M</font>");

        switch (kind)
        {
            case "cpu":
                sb.AppendLine($">**CPU使用率：**<font color=\"red\">{data.CpuRate:p0} >= {node.AlarmCpuRate / 100d:p0}</font>");
                break;
            case "memory":
                var rate1 = 1 - (node.Memory == 0 ? 0 : ((Double)data.AvailableMemory / node.Memory));
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
        var url = setting.WebUrl;
        if (!url.IsNullOrEmpty())
        {
            url = url.EnsureEnd("/") + "Nodes/NodeData?nodeId=" + node.ID;
            str += Environment.NewLine + $"[更多信息]({url})";
        }

        return str;
    }
    #endregion

    #region Redis告警
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
            RobotHelper.SendAlarm(node.Category, node.WebHook, "Redis告警", msg);
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
        var url = setting.WebUrl;
        if (!url.IsNullOrEmpty())
        {
            url = url.EnsureEnd("/") + "Nodes/RedisNode?id=" + node.Id;
            str += Environment.NewLine + $"[更多信息]({url})";
        }

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
                    RobotHelper.SendAlarm(groupName, webhook, "消息队列告警", msg);
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
        var url = setting.WebUrl;
        if (!url.IsNullOrEmpty())
        {
            url = url.EnsureEnd("/") + "Nodes/RedisMessageQueue?redisId=" + queue.RedisId + "&q=" + queue.Name;
            str += Environment.NewLine + $"[更多信息]({url})";
        }

        return str;
    }
    #endregion
}