using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NewLife;
using NewLife.Log;
using NewLife.Security;
using Stardust.Data.Nodes;
using Stardust.Server.Services;
using Xunit;

namespace ServerTest.Services;

/// <summary>MySQL 监控服务测试。覆盖启停生命周期与未启用节点跳过（DBM-6）</summary>
public class MySqlMonitorServiceTests
{
    /// <summary>调用私有的采集循环，模拟一次定时器触发</summary>
    private static void RunTrace(MySqlService service) =>
        typeof(MySqlService).GetMethod("DoTraceNode", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(service, [null]);

    [Fact(DisplayName = "MySQL 监控启停不抛异常")]
    public async Task StartStop_NoThrow()
    {
        var service = new MySqlService(null, Logger.Null) { Period = 60 };

        await service.StartAsync(CancellationToken.None);
        await service.StopAsync(CancellationToken.None);
    }

    [Fact(DisplayName = "未启用节点不参与采集，状态保持不变")]
    public void TraceNode_DisabledNode_Skip()
    {
        var service = new MySqlService(null, Logger.Null);

        var node = new MySqlNode
        {
            Name = "mysql-" + Rand.NextString(6),
            Server = "127.0.0.1",
            Port = 1,
            Enable = false,
        };
        node.Insert();

        try
        {
            RunTrace(service);

            // 直接查库确认仍为停用状态（不会因采集失败被再次写入）
            var dt = MySqlNode.Meta.Session.Query($"select Enable from {MySqlNode.Meta.TableName} where Id={node.Id}");
            Assert.True(dt != null && dt.Rows.Count > 0);
            Assert.False(dt.Rows[0][0].ToBoolean());
        }
        finally
        {
            MySqlNode.FindById(node.Id)?.Delete();
        }
    }
}
