using System;
using NewLife;
using NewLife.Data;
using NewLife.Security;
using Stardust.Data;
using Stardust.Data.Nodes;
using Xunit;

namespace ServerTest.Web;

/// <summary>历史列表连接事件过滤测试</summary>
public class HistoryFilterTests
{
    [Fact(DisplayName = "节点历史默认排除WebSocket连接事件，显式查询仍可见")]
    public void NodeHistory_ExcludeConnectionActions()
    {
        var node = new Node { Code = "hf_" + Rand.NextString(8), Name = "history-" + Rand.NextString(4), Enable = true };
        node.Insert();

        try
        {
            NodeHistory.Create(node, HistoryAction.WebSocketConnected, true, "connected", "test", "127.0.0.1").Insert();
            NodeHistory.Create(node, HistoryAction.Upgrade, true, "upgraded", "test", "127.0.0.1").Insert();

            var list = NodeHistory.Search(node.ID, -1, -1, null, null, default, default, null, new PageParameter { PageSize = 20 }, HistoryAction.ConnectionActions);
            Assert.DoesNotContain(list, e => e.Action == HistoryAction.WebSocketConnected);
            Assert.Contains(list, e => e.Action == HistoryAction.Upgrade);

            var all = NodeHistory.Search(node.ID, -1, -1, null, null, default, default, null, new PageParameter { PageSize = 20 });
            Assert.Equal(2, all.Count);
        }
        finally
        {
            NodeHistory.Delete(NodeHistory._.NodeID == node.ID);
            node.Delete();
        }
    }

    [Fact(DisplayName = "应用历史默认排除WebSocket断连事件，显式查询仍可见")]
    public void AppHistory_ExcludeConnectionActions()
    {
        var app = new App { Name = "hf_" + Rand.NextString(8), Secret = "x", Enable = true };
        app.Insert();

        try
        {
            AppHistory.Create(app, HistoryAction.WebSocketDisconnected, true, "disconnected", "1.0", "test", "127.0.0.1").Insert();
            AppHistory.Create(app, HistoryAction.ApplicationAuthentication, true, "authenticated", "1.0", "test", "127.0.0.1").Insert();

            var list = AppHistory.SearchEx(app.Id, null, null, null, -1, default, default, null, new PageParameter { PageSize = 20 }, HistoryAction.ConnectionActions);
            Assert.DoesNotContain(list, e => e.Action == HistoryAction.WebSocketDisconnected);
            Assert.Contains(list, e => e.Action == HistoryAction.ApplicationAuthentication);

            var all = AppHistory.SearchEx(app.Id, null, null, null, -1, default, default, null, new PageParameter { PageSize = 20 });
            Assert.Equal(2, all.Count);
        }
        finally
        {
            AppHistory.Delete(AppHistory._.AppId == app.Id);
            app.Delete();
        }
    }
}
