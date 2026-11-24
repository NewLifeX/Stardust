using System.ComponentModel;
using Stardust.Data.Nodes;
using Stardust.Server.Services;
using Xunit;

namespace ServerTest;

/// <summary>节点唤醒：代发节点选择规则测试</summary>
public class NodeWakeServiceTests
{
    private static Node CreateTarget(String? gateway = "192.168.1.1/AA-BB-CC-DD-EE-FF", String? ip = "192.168.1.10", String? lastLoginIp = null)
        => new() { ID = 1, Name = "target", Code = "T1", MACs = "AA-BB-CC-DD-EE-FF", Gateway = gateway, IP = ip, LastLoginIP = lastLoginIp };

    private static Node CreateNode(Int32 id, String? gateway, String? ip, String? lastLoginIp = null)
        => new() { ID = id, Name = $"n{id}", Code = $"C{id}", MACs = $"AA-BB-CC-DD-EE-{id:X2}", Gateway = gateway, IP = ip, LastLoginIP = lastLoginIp };

    private static NodeOnline CreateOnline(Int32 nodeId, String? gateway, String? ip, Int32 latency = 0)
        => new() { NodeID = nodeId, Gateway = gateway, IP = ip, GatewayLatency = latency, UpdateTime = DateTime.Now };

    [Fact]
    [DisplayName("优先选择网关MAC相同的节点")]
    public void SelectRelays_SameGatewayMac()
    {
        var target = CreateTarget();
        var nodes = new List<Node>
        {
            CreateNode(2, "192.168.1.1/AA-BB-CC-DD-EE-FF", "192.168.1.20"),
            CreateNode(3, "192.168.1.1/11-22-33-44-55-66", "192.168.1.30"),
            CreateNode(4, "10.0.0.1/22-33-44-55-66-77", "10.0.0.40"),
        };
        var onlines = new List<NodeOnline>
        {
            CreateOnline(2, "192.168.1.1/AA-BB-CC-DD-EE-FF", "192.168.1.20"),
            CreateOnline(3, "192.168.1.1/11-22-33-44-55-66", "192.168.1.30"),
            CreateOnline(4, "10.0.0.1/22-33-44-55-66-77", "10.0.0.40"),
        };

        var relays = NodeWakeService.SelectRelays(target, onlines, nodes);

        var online = Assert.Single(relays);
        Assert.Equal(2, online.NodeID);
    }

    [Fact]
    [DisplayName("网关MAC只存在于在线记录时同样可用")]
    public void SelectRelays_GatewayFromOnline()
    {
        var target = CreateTarget();
        var nodes = new List<Node> { CreateNode(2, null, "192.168.1.20") };
        var onlines = new List<NodeOnline> { CreateOnline(2, "192.168.1.1/aa-bb-cc-dd-ee-ff", "192.168.1.20") };

        var relays = NodeWakeService.SelectRelays(target, onlines, nodes);

        var online = Assert.Single(relays);
        Assert.Equal(2, online.NodeID);
    }

    [Fact]
    [DisplayName("网关MAC缺失时退回网关IP匹配")]
    public void SelectRelays_SameGatewayIp()
    {
        var target = CreateTarget(gateway: "192.168.1.1", lastLoginIp: "1.2.3.4");
        var nodes = new List<Node>
        {
            CreateNode(2, "192.168.1.1/11-22-33-44-55-66", "192.168.1.20"),
            CreateNode(3, "10.0.0.1/22-33-44-55-66-77", "10.0.0.30", "1.2.3.4"),
        };
        var onlines = new List<NodeOnline>
        {
            CreateOnline(2, "192.168.1.1/11-22-33-44-55-66", "192.168.1.20"),
            CreateOnline(3, "10.0.0.1/22-33-44-55-66-77", "10.0.0.30"),
        };

        var relays = NodeWakeService.SelectRelays(target, onlines, nodes);

        var online = Assert.Single(relays);
        Assert.Equal(2, online.NodeID);
    }

    [Fact]
    [DisplayName("网关信息缺失时按公网IP兜底")]
    public void SelectRelays_SamePublicIp()
    {
        var target = CreateTarget(gateway: null, ip: "10.9.9.9", lastLoginIp: "1.2.3.4");
        var nodes = new List<Node>
        {
            CreateNode(2, null, "10.1.1.1", "1.2.3.4"),
            CreateNode(3, null, "10.1.1.2", "5.6.7.8"),
        };
        var onlines = new List<NodeOnline>
        {
            CreateOnline(2, null, "10.1.1.1"),
            CreateOnline(3, null, "10.1.1.2"),
        };

        var relays = NodeWakeService.SelectRelays(target, onlines, nodes);

        var online = Assert.Single(relays);
        Assert.Equal(2, online.NodeID);
    }

    [Fact]
    [DisplayName("网关与公网IP都不匹配时按私有/24兜底")]
    public void SelectRelays_SameSubnet()
    {
        var target = CreateTarget(gateway: null, ip: "192.168.1.10");
        var nodes = new List<Node>
        {
            CreateNode(2, null, "192.168.1.20"),
            CreateNode(3, null, "10.0.0.5"),
        };
        var onlines = new List<NodeOnline>
        {
            CreateOnline(2, null, "192.168.1.20"),
            CreateOnline(3, null, "10.0.0.5"),
        };

        var relays = NodeWakeService.SelectRelays(target, onlines, nodes);

        var online = Assert.Single(relays);
        Assert.Equal(2, online.NodeID);
    }

    [Fact]
    [DisplayName("只选同项目的代发节点")]
    public void SelectRelays_SameProject()
    {
        var target = CreateTarget();
        target.ProjectId = 7;
        var nodes = new List<Node>
        {
            CreateNode(2, "192.168.1.1/AA-BB-CC-DD-EE-FF", "192.168.1.20"),
            CreateNode(3, "192.168.1.1/AA-BB-CC-DD-EE-FF", "192.168.1.30"),
        };
        var onlines = new List<NodeOnline>
        {
            CreateOnline(2, "192.168.1.1/AA-BB-CC-DD-EE-FF", "192.168.1.20"),
            CreateOnline(3, "192.168.1.1/AA-BB-CC-DD-EE-FF", "192.168.1.30"),
        };
        onlines[0].ProjectId = 7;
        onlines[1].ProjectId = 8;

        var relays = NodeWakeService.SelectRelays(target, onlines, nodes);

        var online = Assert.Single(relays);
        Assert.Equal(2, online.NodeID);
    }

    [Fact]
    [DisplayName("排除目标自身和重复会话，且最多返回5个")]
    public void SelectRelays_ExcludeAndLimit()
    {
        var target = CreateTarget();
        var nodes = new List<Node>();
        var onlines = new List<NodeOnline>
        {
            CreateOnline(target.ID, "192.168.1.1/AA-BB-CC-DD-EE-FF", "192.168.1.10"),
            CreateOnline(2, "192.168.1.1/AA-BB-CC-DD-EE-FF", "192.168.1.20", 1),
            CreateOnline(2, "192.168.1.1/AA-BB-CC-DD-EE-FF", "192.168.1.20", 1),
        };
        for (var i = 3; i <= 10; i++)
        {
            nodes.Add(CreateNode(i, "192.168.1.1/AA-BB-CC-DD-EE-FF", $"192.168.1.{i + 10}"));
            onlines.Add(CreateOnline(i, "192.168.1.1/AA-BB-CC-DD-EE-FF", $"192.168.1.{i + 10}", 50));
        }

        var relays = NodeWakeService.SelectRelays(target, onlines, nodes);

        Assert.Equal(5, relays.Count);
        Assert.DoesNotContain(relays, e => e.NodeID == target.ID);
        Assert.Single(relays, e => e.NodeID == 2);
    }

    [Fact]
    [DisplayName("没有任何同网段节点时返回空")]
    public void SelectRelays_NoMatch()
    {
        var target = CreateTarget(gateway: null, ip: "192.168.1.10");
        var nodes = new List<Node> { CreateNode(2, null, "10.0.0.5") };
        var onlines = new List<NodeOnline> { CreateOnline(2, null, "10.0.0.5") };

        var relays = NodeWakeService.SelectRelays(target, onlines, nodes);

        Assert.Empty(relays);
    }
}
