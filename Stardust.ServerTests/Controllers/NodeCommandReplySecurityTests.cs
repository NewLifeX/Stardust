using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using NewLife;
using NewLife.Remoting;
using NewLife.Remoting.Models;
using Stardust.Data.Nodes;
using Stardust.Server;
using Xunit;

namespace ServerTest.Controllers;

/// <summary>节点命令回包归属安全测试。验证节点只能回包属于自己的命令（安全扫描 V-17）</summary>
[Collection(StarTestServerCollection.Name)]
public class NodeCommandReplySecurityTests : StarTestBase
{
    public NodeCommandReplySecurityTests(StarTestServer fixture) : base(fixture) { }

    private async Task<(String token, Node node)> LoginNodeAsync()
    {
        var client = _server.CreateClient();
        var rs = await client.PostAsync<LoginResponse>("node/login", new
        {
            code = "",
            node = new
            {
                MachineName = "test_reply_sec",
                macs = "xxyyzz" + Guid.NewGuid().ToString("N")[..6],
            },
        });
        Assert.NotNull(rs?.Token);
        var node = Node.FindByCode(rs?.Code);
        Assert.NotNull(node);
        return (rs.Token, node);
    }

    private async Task<String> CommandReplyAsync(String token, Int32 cmdId)
    {
        var client = _server.CreateClient();
        client.DefaultRequestHeaders.Add("X-Token", token);

        var model = new { id = cmdId, status = (Int32)CommandStatus.已完成, data = "ok" };
        var response = await client.PostAsync("/Node/CommandReply", JsonContent.Create(model));
        return await response.Content.ReadAsStringAsync();
    }

    [Fact(DisplayName = "节点不能回包属于其它节点的命令")]
    public async Task ReplyOtherNodeCommand_Rejected()
    {
        var (tokenA, nodeA) = await LoginNodeAsync();

        // 另一节点 B 及其命令
        var nodeB = new Node { Code = "nb_" + Guid.NewGuid().ToString("N")[..6], Name = "v17node", Enable = true };
        nodeB.Insert();
        var cmdB = new NodeCommand { NodeID = nodeB.ID, Command = "bash", Status = CommandStatus.就绪 };
        cmdB.Insert();
        try
        {
            var body = await CommandReplyAsync(tokenA, cmdB.Id);

            Assert.Contains("\"code\"", body);
            Assert.Contains("越权", body);

            // 命令状态不应被篡改
            var after = NodeCommand.FindById(cmdB.Id);
            Assert.Equal(CommandStatus.就绪, after.Status);
        }
        finally
        {
            cmdB.Delete();
            nodeB.Delete();
            nodeA.Delete();
        }
    }

    [Fact(DisplayName = "节点回包自己的命令成功（回归保护）")]
    public async Task ReplyOwnCommand_Ok()
    {
        var (token, node) = await LoginNodeAsync();
        var cmd = new NodeCommand { NodeID = node.ID, Command = "bash", Status = CommandStatus.就绪 };
        cmd.Insert();
        try
        {
            var body = await CommandReplyAsync(token, cmd.Id);

            Assert.DoesNotContain("越权", body);

            var after = NodeCommand.FindById(cmd.Id);
            Assert.Equal(CommandStatus.已完成, after.Status);
        }
        finally
        {
            cmd.Delete();
            node.Delete();
        }
    }
}
