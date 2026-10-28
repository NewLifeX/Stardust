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
}
