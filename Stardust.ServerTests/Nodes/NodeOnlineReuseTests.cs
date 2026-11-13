using System;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using NewLife.Remoting.Models;
using Stardust.Data.Nodes;
using Stardust.Server.Services;
using Xunit;

namespace ServerTest.Nodes;

/// <summary>弱网重连时的在线记录复用测试。会话标识变化不应每次都新建在线记录</summary>
[Collection(StarTestServerCollection.Name)]
public class NodeOnlineReuseTests : StarTestBase
{
    public NodeOnlineReuseTests(StarTestServer fixture) : base(fixture) { }

    /// <summary>绕过构造函数创建 NodeService，仅测试 GetOnline 的复用规则</summary>
    private static NodeService CreateService() => (NodeService)RuntimeHelpers.GetUninitializedObject(typeof(NodeService));

    private static Node CreateNode()
    {
        var code = "onl_" + Guid.NewGuid().ToString("N")[..8];
        var node = new Node { Code = code, Name = code, Enable = true };
        node.Insert();

        return node;
    }

    private static NodeOnline CreateOnline(Node node, String sessionId)
    {
        var online = new NodeOnline
        {
            SessionID = sessionId,
            NodeID = node.ID,
            Name = node.Name,
            PingCount = 7,
        };
        online.Insert();

        return online;
    }

    private static void Cleanup(Node node)
    {
        foreach (var e in NodeOnline.FindAllByNodeId(node.ID)) e.Delete();
        node.Delete();
    }

    [Fact(DisplayName = "会话标识变化时，宽限期内沿用同一节点的在线记录")]
    public void GetOnline_ReusesRecentRecord()
    {
        var node = CreateNode();
        try
        {
            var online = CreateOnline(node, "old@127.0.0.1");

            var svc = CreateService();
            svc.OnlineReuseSeconds = 900;
            var context = new DeviceContext { Device = node, ClientId = "new@127.0.0.1", UserHost = "127.0.0.1" };

            var rs = svc.GetOnline(context);

            var reuse = Assert.IsType<NodeOnline>(rs);
            Assert.Equal(online.ID, reuse.ID);
            Assert.Equal("new@127.0.0.1", reuse.SessionID);
            // 复用记录，心跳计数不被重置
            Assert.Equal(7, reuse.PingCount);
        }
        finally
        {
            Cleanup(node);
        }
    }

    [Fact(DisplayName = "超过宽限期后不再沿用旧记录，按真正离线处理")]
    public void GetOnline_ExpiredRecord_NotReused()
    {
        var node = CreateNode();
        try
        {
            CreateOnline(node, "old@127.0.0.1");

            var svc = CreateService();
            svc.OnlineReuseSeconds = -1;
            var context = new DeviceContext { Device = node, ClientId = "new@127.0.0.1", UserHost = "127.0.0.1" };

            Assert.Null(svc.GetOnline(context));
        }
        finally
        {
            Cleanup(node);
        }
    }

    [Fact(DisplayName = "会话标识未变时直接命中已有在线记录")]
    public void GetOnline_SameSession_HitsRecord()
    {
        var node = CreateNode();
        try
        {
            var online = CreateOnline(node, "same@127.0.0.1");

            var svc = CreateService();
            svc.OnlineReuseSeconds = 900;
            var context = new DeviceContext { Device = node, ClientId = "same@127.0.0.1", UserHost = "127.0.0.1" };

            var rs = Assert.IsType<NodeOnline>(svc.GetOnline(context));
            Assert.Equal(online.ID, rs.ID);
        }
        finally
        {
            Cleanup(node);
        }
    }
}
