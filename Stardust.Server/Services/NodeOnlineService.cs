using NewLife;
using NewLife.Log;
using NewLife.Threading;
using Stardust.Data;
using Stardust.Data.Nodes;

namespace Stardust.Server.Services;

/// <summary>节点在线服务</summary>
public class NodeOnlineService : IHostedService
{
    #region 属性
    private TimerX _timer;
    private NodeService _nodeService;
    private readonly IServiceProvider _serviceProvider;
    private readonly StarServerSetting _setting;
    private readonly ITracer _tracer;
    #endregion

    #region 构造
    public NodeOnlineService(IServiceProvider serviceProvider, StarServerSetting setting, ITracer tracer)
    {
        //_nodeService = nodeService;
        _serviceProvider = serviceProvider;
        _setting = setting;
        _tracer = tracer;
    }
    #endregion

    #region 方法
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _timer = new TimerX(CheckNodeOnline, null, 15_000, 30_000) { Async = true };

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
        var set = _setting;
        var sessionTimeout = set.SessionTimeout;
        if (sessionTimeout > 0)
        {
            using var span = _tracer?.NewSpan(nameof(CheckNodeOnline));
            _nodeService ??= _serviceProvider.GetService<NodeService>();

            var rs = NodeOnline.ClearExpire(TimeSpan.FromSeconds(sessionTimeout));
            if (rs != null)
            {
                foreach (var olt in rs)
                {
                    var node = olt?.Node;
                    var msg = $"[{node}]登录于{olt.CreateTime}，最后活跃于{olt.UpdateTime}";
                    node.WriteHistory("超时下线", true, msg, olt.CreateIP);

                    if (node != null)
                    {
                        // 计算在线时长
                        if (olt.CreateTime.Year > 2000 && olt.UpdateTime.Year > 2000)
                        {
                            node.OnlineTime += (Int32)(olt.UpdateTime - olt.CreateTime).TotalSeconds;
                            node.Update();
                        }

                        _nodeService.RemoveOnline(node);

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