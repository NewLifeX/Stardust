using NewLife;
using NewLife.Log;
using NewLife.Remoting.Models;
using NewLife.Threading;
using Stardust.Data;
using Stardust.Data.Nodes;

namespace Stardust.Server.Services;

/// <summary>节点在线服务</summary>
public class NodeOnlineService(IServiceProvider serviceProvider, StarServerSetting setting, ITracer tracer) : IHostedService
{
    #region 属性
    private TimerX _timer;
    private NodeService _nodeService;
    #endregion

    #region 方法
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _timer = new TimerX(CheckNodeOnline, null, 30_000, 30_000) { Async = true };

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _timer.TryDispose();

        return Task.CompletedTask;
    }

    private void CheckNodeOnline(Object state)
    {
        // 节点超时
        var sessionTimeout = setting.SessionTimeout;
        if (sessionTimeout > 0)
        {
            using var span = tracer?.NewSpan(nameof(CheckNodeOnline));
            _nodeService ??= serviceProvider.GetService<NodeService>();

            var rs = NodeOnline.ClearExpire(TimeSpan.FromSeconds(sessionTimeout));
            if (rs != null)
            {
                foreach (var online in rs)
                {
                    var node = online?.Node;
                    if (node != null)
                    {
                        var msg = $"[{node}/{online?.SessionID}]登录于{online.CreateTime}，最后活跃于{online.UpdateTime}";
                        node.WriteHistory("超时下线", true, msg, online.CreateIP);

                        // 计算在线时长
                        if (online.CreateTime.Year > 2000 && online.UpdateTime.Year > 2000)
                        {
                            node.OnlineTime += (Int32)(online.UpdateTime - online.CreateTime).TotalSeconds;
                            node.Update();
                        }

                        _nodeService.RemoveOnline(new DeviceContext { Device = node, Online = online });

                        CheckOffline(node, "超时下线");
                    }
                }
            }
        }
    }

    public static void CheckOffline(Node node, String reason)
    {
        // 下线告警
        if (node.AlarmOnOffline)
        {
            var webhook = RobotHelper.GetAlarm(node.Project, node.Category, node.WebHook);
            if (webhook.IsNullOrEmpty()) return;

            // 查找该节点还有没有其它实例在线
            var olts = NodeOnline.FindAllByNodeId(node.ID);
            if (olts.Count == 0)
            {
                var msg = $"节点[{node.Name}]已下线！{reason} IP={node.IP}";
                RobotHelper.SendAlarm(node.Category, node.WebHook, "节点下线告警", msg);
            }
        }
    }
    #endregion
}