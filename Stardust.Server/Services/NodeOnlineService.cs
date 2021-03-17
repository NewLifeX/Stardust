using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using NewLife;
using NewLife.Log;
using NewLife.Threading;
using Stardust.Data.Nodes;
using Stardust.DingTalk;
using Stardust.WeiXin;

namespace Stardust.Server.Services
{
    /// <summary>节点在线服务</summary>
    public class NodeOnlineService : IHostedService
    {
        #region 属性
        private TimerX _timer;
        private readonly ITracer _tracer;
        #endregion

        #region 构造
        public NodeOnlineService(ITracer tracer) => _tracer = tracer;
        #endregion

        #region 方法
        public Task StartAsync(CancellationToken cancellationToken)
        {
            _timer = new TimerX(CheckOnline, null, 5_000, 30_000) { Async = true };

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _timer.TryDispose();

            return Task.CompletedTask;
        }

        private void CheckOnline(Object state)
        {
            var set = Setting.Current;
            if (set.SessionTimeout > 0)
            {
                using var span = _tracer?.NewSpan(nameof(CheckOnline));

                var rs = NodeOnline.ClearExpire(set.SessionTimeout);
                if (rs != null)
                {
                    foreach (var olt in rs)
                    {
                        var node = olt?.Node;
                        var msg = $"[{node}]登录于{olt.CreateTime}，最后活跃于{olt.UpdateTime}";
                        NodeHistory.Create(node, "超时下线", true, msg, Environment.MachineName, olt.CreateIP);

                        if (node != null)
                        {
                            // 计算在线时长
                            if (olt.CreateTime.Year > 2000 && olt.UpdateTime.Year > 2000)
                            {
                                node.OnlineTime += (Int32)(olt.UpdateTime - olt.CreateTime).TotalSeconds;
                                node.SaveAsync();
                            }

                            CheckOffline(node);
                        }
                    }
                }
            }
        }

        public static void CheckOffline(Node node)
        {
            // 下线告警
            if (node.AlarmOnOffline && !node.WebHook.IsNullOrEmpty())
            {
                // 查找该节点还有没有其它实例在线
                var olts = NodeOnline.FindAllByNodeId(node.ID);
                if (olts.Count == 0)
                {
                    var msg = $"节点[{node.Name}]已下线！IP={node.IP}";
                    SendAlarm(node.WebHook, "节点下线告警", msg);
                }
            }
        }

        private static void SendAlarm(String robot, String title, String msg)
        {
            XTrace.WriteLine(msg);

            if (robot.Contains("qyapi.weixin"))
            {
                var weixin = new WeiXinClient { Url = robot };

                using var span = DefaultTracer.Instance?.NewSpan("SendWeixin", msg);
                weixin.SendMarkDown(msg);
            }
            else if (robot.Contains("dingtalk"))
            {
                var dingTalk = new DingTalkClient { Url = robot };

                using var span = DefaultTracer.Instance?.NewSpan("SendDingTalk", msg);
                dingTalk.SendMarkDown(title, msg, null);
            }
        }
        #endregion
    }
}