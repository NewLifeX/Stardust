using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NewLife;
using NewLife.Caching;
using NewLife.Threading;
using Stardust.Data.Monitors;
using Stardust.Data.Nodes;
using Stardust.DingTalk;
using Stardust.WeiXin;

namespace Stardust.Server.Services
{
    public interface IAlarmService
    {
        /// <summary>添加需要统计的应用，去重</summary>
        /// <param name="appId"></param>
        void Add(Int32 appId);
    }

    public class AlarmService : IAlarmService
    {
        /// <summary>计算周期。默认30秒</summary>
        public Int32 Period { get; set; } = 30;

        private readonly TimerX _timer;
        private readonly ConcurrentBag<Int32> _bag = new ConcurrentBag<Int32>();
        //private WeiXinClient _weixin;
        //private DingTalkClient _dingTalk;
        private readonly ICache _cache = new MemoryCache();

        public AlarmService() =>
            // 初始化定时器
            _timer = new TimerX(DoAlarm, null, 5_000, Period * 1000) { Async = true };

        /// <summary>添加需要统计的应用，去重</summary>
        /// <param name="appId"></param>
        public void Add(Int32 appId)
        {
            if (!_bag.Contains(appId)) _bag.Add(appId);
        }

        private void DoAlarm(Object state)
        {
            while (_bag.TryTake(out var appId))
            {
                //Process(appId);
            }

            // 应用告警
            var list = AppTracer.FindAllWithCache();
            foreach (var item in list)
            {
                ProcessAppTracer(item);
            }

            // 节点告警
            var nodes = Node.FindAllWithCache();
            foreach (var item in nodes)
            {
                ProcessNode(item);
            }

            // Redis告警
            var rnodes = RedisNode.FindAllWithCache();
            foreach (var item in rnodes)
            {
                ProcessRedisNode(item);
            }

            if (Period > 0) _timer.Period = Period * 1000;
        }

        #region 应用性能跟踪告警
        private void ProcessAppTracer(AppTracer app)
        {
            // 应用是否需要告警
            //var app = AppTracer.FindByID(appId);
            if (app == null || !app.Enable || app.AlarmThreshold <= 0) return;

            var appId = app.ID;
            var robot = app.AlarmRobot;
            if (robot.IsNullOrEmpty()) return;

            // 最近一段时间的5分钟级数据
            var time = DateTime.Now;
            var minute = time.Date.AddHours(time.Hour).AddMinutes(time.Minute / 5 * 5);
            var st = AppMinuteStat.FindByAppIdAndTime(appId, minute);
            if (st == null) return;

            // 判断告警
            if (st.Errors >= app.AlarmThreshold)
            {
                // 一定时间内不要重复报错，除非错误翻倍
                var error2 = _cache.Get<Int32>("alarm:AppTracer:" + appId);
                if (error2 == 0 || st.Errors > error2 * 2)
                {
                    _cache.Set("alarm:AppTracer:" + appId, st.Errors, 5 * 60);

                    if (robot.Contains("qyapi.weixin"))
                        SendWeixin(app, st);
                    else if (robot.Contains("dingtalk"))
                        SendDingTalk(app, st);
                }
            }
        }

        private static String GetMarkdown(AppTracer app, AppMinuteStat st, Boolean includeTitle)
        {
            var sb = new StringBuilder();
            if (includeTitle) sb.AppendLine($"### [{app}]系统告警");
            sb.AppendLine($">**总数：**<font color=\"info\">{st.Errors}</font>");

            // 找找具体接口错误
            var names = new List<String>();
            var sts = TraceMinuteStat.FindAllByAppIdAndTime(st.AppId, st.StatTime).OrderByDescending(e => e.Errors).ToList();
            foreach (var item in sts)
            {
                if (item.Errors > 0)
                {
                    sb.AppendLine($">**错误：**<font color=\"info\">{item.StatTime.ToFullString()} 埋点[{item.Name}]共报错[{item.Errors:n0}]次</font>");

                    // 相同接口的错误，不要报多次
                    if (!names.Contains(item.Name))
                    {
                        var ds = TraceData.Search(st.AppId, item.Name, "minute", item.StatTime, 20);
                        if (ds.Count > 0)
                        {
                            var sms = SampleData.FindAllByDataIds(ds.Select(e => e.Id).ToArray()).Where(e => !e.Error.IsNullOrEmpty()).ToList();
                            if (sms.Count > 0)
                            {
                                var msg = sms[0].Error?.Trim();
                                if (!msg.IsNullOrEmpty())
                                {
                                    // 错误内容取第一行，详情看更多
                                    var p = msg.IndexOf(Environment.NewLine);
                                    if (p > 0) msg = msg.Substring(0, p);

                                    sb.AppendLine($">**错误内容：**{msg}");

                                    names.Add(item.Name);
                                }
                            }
                        }
                    }
                }
            }

            var str = sb.ToString();
            if (str.Length > 2000) str = str.Substring(0, 2000);

            // 构造网址
            var url = Setting.Current.WebUrl;
            if (!url.IsNullOrEmpty())
            {
                url = url.EnsureEnd("/") + "Monitors/appMinuteStat?appId=" + st.AppId;
                str += Environment.NewLine + $"[更多信息]({url})";
            }

            return str;
        }

        private void SendWeixin(AppTracer app, AppMinuteStat st)
        {
            var _weixin = new WeiXinClient { Url = app.AlarmRobot };

            var msg = GetMarkdown(app, st, true);

            _weixin.SendMarkDown(msg);
        }

        private void SendDingTalk(AppTracer app, AppMinuteStat st)
        {
            var _dingTalk = new DingTalkClient { Url = app.AlarmRobot };

            var msg = GetMarkdown(app, st, false);

            _dingTalk.SendMarkDown("系统告警", msg, null);
        }
        #endregion

        #region 节点告警
        private void ProcessNode(Node node)
        {
            if (node == null || !node.Enable || node.WebHook.IsNullOrEmpty()) return;

            var robot = node.WebHook;
            if (node.AlarmCpuRate <= 0 && node.AlarmMemoryRate <= 0 && node.AlarmDiskRate <= 0) return;

            // 最新数据
            var data = NodeData.FindLast(node.ID);
            if (data == null) return;

            // CPU告警
            if (node.AlarmCpuRate > 0)
            {
                var rate = data.CpuRate;
                if (rate >= node.AlarmCpuRate)
                {
                    // 一定时间内不要重复报错，除非错误翻倍
                    var error2 = _cache.Get<Int32>("alarm:CpuRate:" + node.ID);
                    if (error2 == 0 || rate > error2 * 2)
                    {
                        _cache.Set("alarm:CpuRate:" + node.ID, rate, 5 * 60);

                        var title = $"[{node}]CPU告警";
                        if (robot.Contains("qyapi.weixin"))
                        {
                            var weixin = new WeiXinClient { Url = robot };
                            var msg = GetMarkdown("cpu", node, data, title);
                            weixin.SendMarkDown(msg);
                        }
                        else if (robot.Contains("dingtalk"))
                        {
                            var dingTalk = new DingTalkClient { Url = robot };
                            var msg = GetMarkdown("cpu", node, data, null);
                            dingTalk.SendMarkDown(title, msg, null);
                        }
                    }
                }
            }

            // 内存告警
            if (node.AlarmMemoryRate > 0 && node.Memory > 0)
            {
                var rate = data.AvailableMemory / node.Memory;
                if (rate >= node.AlarmMemoryRate)
                {
                    // 一定时间内不要重复报错，除非错误翻倍
                    var error2 = _cache.Get<Int32>("alarm:MemoryRate:" + node.ID);
                    if (error2 == 0 || rate > error2 * 2)
                    {
                        _cache.Set("alarm:MemoryRate:" + node.ID, rate, 5 * 60);

                        var title = $"[{node}]内存告警";
                        if (robot.Contains("qyapi.weixin"))
                        {
                            var weixin = new WeiXinClient { Url = robot };
                            var msg = GetMarkdown("memory", node, data, title);
                            weixin.SendMarkDown(msg);
                        }
                        else if (robot.Contains("dingtalk"))
                        {
                            var dingTalk = new DingTalkClient { Url = robot };
                            var msg = GetMarkdown("memory", node, data, null);
                            dingTalk.SendMarkDown(title, msg, null);
                        }
                    }
                }
            }

            // 磁盘告警
            if (node.AlarmDiskRate > 0 && node.TotalSize > 0)
            {
                var rate = data.AvailableFreeSpace / node.TotalSize;
                if (rate >= node.AlarmDiskRate)
                {
                    // 一定时间内不要重复报错，除非错误翻倍
                    var error2 = _cache.Get<Int32>("alarm:DiskRate:" + node.ID);
                    if (error2 == 0 || rate > error2 * 2)
                    {
                        _cache.Set("alarm:DiskRate:" + node.ID, rate, 5 * 60);

                        var title = $"[{node}]磁盘告警";
                        if (robot.Contains("qyapi.weixin"))
                        {
                            var weixin = new WeiXinClient { Url = robot };
                            var msg = GetMarkdown("disk", node, data, title);
                            weixin.SendMarkDown(msg);
                        }
                        else if (robot.Contains("dingtalk"))
                        {
                            var dingTalk = new DingTalkClient { Url = robot };
                            var msg = GetMarkdown("disk", node, data, null);
                            dingTalk.SendMarkDown(title, msg, null);
                        }
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

                        var title = $"[{node}]Tcp告警";
                        if (robot.Contains("qyapi.weixin"))
                        {
                            var weixin = new WeiXinClient { Url = robot };
                            var msg = GetMarkdown("tcp", node, data, title);
                            weixin.SendMarkDown(msg);
                        }
                        else if (robot.Contains("dingtalk"))
                        {
                            var dingTalk = new DingTalkClient { Url = robot };
                            var msg = GetMarkdown("tcp", node, data, null);
                            dingTalk.SendMarkDown(title, msg, null);
                        }
                    }
                }
            }
        }

        private static String GetMarkdown(String kind, Node node, NodeData data, String title)
        {
            var sb = new StringBuilder();
            if (!title.IsNullOrEmpty()) sb.AppendLine($"### [{node}]Redis内存告警");
            sb.AppendLine($">**节点：**<font color=\"info\">{node.Name} / {node.IP}</font>");
            sb.AppendLine($">**分类：**<font color=\"info\">{node.Category}</font>");
            sb.AppendLine($">**系统：**<font color=\"info\">{node.OS}</font>");
            sb.AppendLine($">**CPU核心：**<font color=\"info\">{node.Cpu}</font>");
            sb.AppendLine($">**内存容量：**<font color=\"info\">{node.Memory:n0}M</font>");
            sb.AppendLine($">**磁盘容量：**<font color=\"info\">{node.TotalSize:n0}M</font>");

            switch (kind)
            {
                case "cpu":
                    sb.AppendLine($">**CPU使用率：**<font color=\"info\">{data.CpuRate:p0} >= {node.AlarmCpuRate:p0}</font>");
                    break;
                case "memory":
                    var memory = data.AvailableMemory;
                    var rate1 = 1 - (node.Memory == 0 ? 0 : (data.AvailableMemory / node.Memory));
                    sb.AppendLine($">**内存使用率：**<font color=\"info\">{rate1:p0} >= {node.AlarmMemoryRate:p0}，可用{memory:n0}M</font>");
                    break;
                case "disk":
                    var disk = data.AvailableFreeSpace;
                    var rate2 = 1 - (node.TotalSize == 0 ? 0 : (data.AvailableFreeSpace / node.TotalSize));
                    sb.AppendLine($">**磁盘使用率：**<font color=\"info\"> {rate2:p0} >= {node.AlarmDiskRate:p0}，可用{disk:n0}M</font>");
                    break;
                case "tcp":
                    if (data.TcpConnections >= node.AlarmTcp)
                        sb.AppendLine($">**TCP连接数：**<font color=\"info\">{data.TcpConnections:n0} >= {node.AlarmTcp:n0}</font>");
                    if (data.TcpTimeWait >= node.AlarmTcp)
                        sb.AppendLine($">**TCP主动关闭：**<font color=\"info\">{data.TcpTimeWait:n0} >= {node.AlarmTcp:n0}</font>");
                    if (data.TcpCloseWait >= node.AlarmTcp)
                        sb.AppendLine($">**TCP被动关闭：**<font color=\"info\">{data.TcpCloseWait:n0} >= {node.AlarmTcp:n0}</font>");
                    break;
            }

            var str = sb.ToString();
            if (str.Length > 2000) str = str.Substring(0, 2000);

            // 构造网址
            var url = Setting.Current.WebUrl;
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
            if (node == null || !node.Enable || node.WebHook.IsNullOrEmpty()) return;

            ProcessRedisData(node);
            ProcessRedisQueue(node);
        }

        private void ProcessRedisData(RedisNode node)
        {
            var robot = node.WebHook;
            if (robot.IsNullOrEmpty() || node.AlarmMemoryRate <= 0 || node.MaxMemory <= 0) return;

            // 最新数据
            var data = RedisData.FindLast(node.Id);
            if (data == null) return;

            // 判断告警
            var rate = data.UsedMemory * 100 / node.MaxMemory;
            if (rate >= node.AlarmMemoryRate)
            {
                // 一定时间内不要重复报错，除非错误翻倍
                var error2 = _cache.Get<Int32>("alarm:RedisMemory:" + node.Id);
                if (error2 == 0 || rate > error2 * 2)
                {
                    _cache.Set("alarm:RedisMemory:" + node.Id, rate, 5 * 60);

                    if (robot.Contains("qyapi.weixin"))
                    {
                        var _weixin = new WeiXinClient { Url = robot };

                        var msg = GetMarkdown(node, data, true);

                        _weixin.SendMarkDown(msg);
                    }
                    else if (robot.Contains("dingtalk"))
                    {
                        var _dingTalk = new DingTalkClient { Url = robot };

                        var msg = GetMarkdown(node, data, false);

                        _dingTalk.SendMarkDown("Redis内存告警", msg, null);
                    }
                }
            }
        }

        private static String GetMarkdown(RedisNode node, RedisData data, Boolean includeTitle)
        {
            var sb = new StringBuilder();
            if (includeTitle) sb.AppendLine($"### [{node}]Redis内存告警");
            sb.AppendLine($">**分类：**<font color=\"info\">{node.Category}</font>");
            sb.AppendLine($">**版本：**<font color=\"info\">{node.Version}</font>");
            sb.AppendLine($">**已用内存：**<font color=\"info\">{data.UsedMemory:n0}</font>");
            sb.AppendLine($">**内存容量：**<font color=\"info\">{node.MaxMemory:n0}</font>");
            sb.AppendLine($">**服务器：**<font color=\"info\">{node.Server}</font>");

            var str = sb.ToString();
            if (str.Length > 2000) str = str.Substring(0, 2000);

            // 构造网址
            var url = Setting.Current.WebUrl;
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
            // 所有队列
            var list = RedisMessageQueue.FindAllByRedisId(node.Id);
            foreach (var queue in list)
            {
                var robot = queue.WebHook;
                if (robot.IsNullOrEmpty()) robot = node.WebHook;

                // 判断告警
                if (!robot.IsNullOrEmpty() && queue.Enable && queue.MaxMessages > 0 && queue.Messages >= queue.MaxMessages)
                {
                    // 一定时间内不要重复报错，除非错误翻倍
                    var error2 = _cache.Get<Int32>("alarm:RedisMessageQueue:" + queue.Id);
                    if (error2 == 0 || queue.Messages > error2 * 2)
                    {
                        _cache.Set("alarm:RedisMessageQueue:" + queue.Id, queue.Messages, 5 * 60);

                        if (robot.Contains("qyapi.weixin"))
                        {
                            var _weixin = new WeiXinClient { Url = robot };

                            var msg = GetMarkdown(node, queue, true);

                            _weixin.SendMarkDown(msg);
                        }
                        else if (robot.Contains("dingtalk"))
                        {
                            var _dingTalk = new DingTalkClient { Url = robot };

                            var msg = GetMarkdown(node, queue, false);

                            _dingTalk.SendMarkDown("消息队列告警", msg, null);
                        }
                    }
                }
            }
        }

        private static String GetMarkdown(RedisNode node, RedisMessageQueue queue, Boolean includeTitle)
        {
            var sb = new StringBuilder();
            if (includeTitle) sb.AppendLine($"### [{queue.Name}/{node}]消息队列告警");
            sb.AppendLine($">**主题：**<font color=\"info\">{queue.Topic}</font>");
            sb.AppendLine($">**积压：**<font color=\"info\">{queue.Messages:n0} > {queue.MaxMessages:n0}</font>");
            sb.AppendLine($">**消费者：**<font color=\"info\">{queue.Consumers}</font>");
            sb.AppendLine($">**总消费：**<font color=\"info\">{queue.Total:n0}</font>");
            sb.AppendLine($">**服务器：**<font color=\"info\">{node.Server}</font>");

            var str = sb.ToString();
            if (str.Length > 2000) str = str.Substring(0, 2000);

            // 构造网址
            var url = Setting.Current.WebUrl;
            if (!url.IsNullOrEmpty())
            {
                url = url.EnsureEnd("/") + "Nodes/RedisMessageQueue?redisId=" + queue.RedisId;
                str += Environment.NewLine + $"[更多信息]({url})";
            }

            return str;
        }
        #endregion
    }
}