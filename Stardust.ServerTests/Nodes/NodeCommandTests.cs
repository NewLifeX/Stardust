using System;
using System.ComponentModel;
using NewLife.Remoting.Models;
using Stardust.Data.Nodes;
using Xunit;

namespace ServerTest.Nodes;

/// <summary>节点命令测试</summary>
public class NodeCommandTests
{
    [Fact]
    [DisplayName("过期未执行的节点命令会被取消，避免永久停留在就绪")]
    public void CancelExpired_CancelsStaleCommands()
    {
        var cmd = new NodeCommand
        {
            NodeID = 990001,
            Command = "test/expired",
            Expire = DateTime.Now.AddMinutes(-1),
            Status = CommandStatus.就绪,
        };
        cmd.Insert();

        try
        {
            var count = NodeCommand.CancelExpired(DateTime.Now);
            Assert.True(count >= 1);

            var found = NodeCommand.FindById(cmd.Id);
            Assert.NotNull(found);
            Assert.Equal(CommandStatus.取消, found.Status);
            Assert.Equal("命令已过期", found.Result);
        }
        finally
        {
            cmd.Delete();
        }
    }
}
