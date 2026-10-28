using System;
using System.Threading;
using System.Threading.Tasks;
using NewLife.Security;
using Stardust.Data.Nodes;
using Stardust.Server;
using Stardust.Server.Services;
using Xunit;

namespace ServerTest.Services;

/// <summary>节点在线服务测试。覆盖定时器生命周期与离线告警门控（NODE-3）</summary>
public class NodeOnlineServiceTests
{
    private static Node CreateNode(Boolean alarmOnOffline) => new()
    {
        Code = "nd_" + Rand.NextString(8),
        Name = "node-" + Rand.NextString(4),
        Enable = true,
        AlarmOnOffline = alarmOnOffline,
    };

    [Fact(DisplayName = "超时判定为零时启动与停止服务不抛异常")]
    public async Task StartStop_NoThrow()
    {
        var svc = new NodeOnlineService(null, new StarServerSetting { SessionTimeout = 0 }, null);

        await svc.StartAsync(CancellationToken.None);
        await svc.StopAsync(CancellationToken.None);
    }

    [Fact(DisplayName = "未开启离线告警时上线检查直接跳过")]
    public void CheckOnline_NoAlarm_Skip()
    {
        var node = CreateNode(false);

        // 未开启告警时直接返回，不产生任何外部调用
        NodeOnlineService.CheckOnline(node);
    }

    [Fact(DisplayName = "未开启离线告警时下线检查直接跳过")]
    public void CheckOffline_NoAlarm_Skip()
    {
        var node = CreateNode(false);

        NodeOnlineService.CheckOffline(node, "test");
    }

    [Fact(DisplayName = "开启告警但未配置Webhook时下线检查安全跳过")]
    public void CheckOffline_AlarmWithoutWebhook_Skip()
    {
        var node = CreateNode(true);
        node.WebHook = null;

        // 没有 Webhook 时获取告警配置为空，直接返回，不发送告警
        NodeOnlineService.CheckOffline(node, "test");
    }

    [Fact(DisplayName = "空节点传入下线检查不抛异常")]
    public void CheckOffline_NullNode_NoThrow() => NodeOnlineService.CheckOffline(null, "test");

    [Fact(DisplayName = "超时清理只删除超过阈值未更新的在线记录")]
    public void ClearExpire_RemovesStaleOnly()
    {
        var node = CreateNode(false);
        node.Insert();

        var stale = new NodeOnline { SessionID = "sess_" + Rand.NextString(8), NodeID = node.ID };
        stale.Insert();
        var fresh = new NodeOnline { SessionID = "sess_" + Rand.NextString(8), NodeID = node.ID };
        fresh.Insert();

        try
        {
            // 绕过时间拦截器，把其中一条的更新时间改到很早
            var old = DateTime.Now.AddHours(-2);
            NodeOnline.Meta.Session.Execute($"Update {NodeOnline.Meta.TableName} Set UpdateTime='{old:yyyy-MM-dd HH:mm:ss}' Where ID={stale.ID}");

            var list = NodeOnline.ClearExpire(TimeSpan.FromMinutes(10));

            Assert.NotNull(list);
            Assert.Contains(list, e => e.ID == stale.ID);
            Assert.Null(NodeOnline.FindById(stale.ID));
            Assert.NotNull(NodeOnline.FindById(fresh.ID));
        }
        finally
        {
            NodeOnline.FindById(stale.ID)?.Delete();
            NodeOnline.FindById(fresh.ID)?.Delete();
            node.Delete();
        }
    }
}

