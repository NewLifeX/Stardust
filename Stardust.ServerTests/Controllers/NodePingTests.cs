using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using NewLife.Remoting;
using NewLife.Remoting.Models;
using NewLife.Security;
using Stardust.Data.Nodes;
using Stardust.Server;
using Xunit;

namespace ServerTest.Controllers;

/// <summary>节点心跳保活测试。覆盖 node/ping 的令牌鉴权与心跳响应字段（与应用心跳同型）</summary>
[Collection(StarTestServerCollection.Name)]
public class NodePingTests : StarTestBase
{
    public NodePingTests(StarTestServer fixture) : base(fixture) { }

    private async Task<(LoginResponse rs, Node node)> LoginNodeAsync(String machine)
    {
        var client = _server.CreateClient();

        // code 留空 => 服务端自动分配节点编码
        var rs = await client.PostAsync<LoginResponse>("node/login", new
        {
            code = "",
            node = new
            {
                MachineName = machine,
                macs = "aabbcc" + Guid.NewGuid().ToString("N")[..6],
            },
        });
        Assert.NotNull(rs?.Token);

        var node = Node.FindByCode(rs!.Code);
        Assert.NotNull(node);

        return (rs, node!);
    }

    [Fact(DisplayName = "节点登录后心跳成功并返回心跳周期")]
    public async Task Ping_AfterLogin_ReturnsPeriod()
    {
        var (rs, node) = await LoginNodeAsync("test_ping_" + Rand.NextString(6));
        try
        {
            var client = _server.CreateClient();
            client.DefaultRequestHeaders.Add("X-Token", rs.Token);

            var model = new { MachineName = node.MachineName, Macs = node.MACs };
            var resp = await client.PostAsync("node/ping", JsonContent.Create(model));
            var body = await resp.Content.ReadAsStringAsync();

            Assert.True(resp.IsSuccessStatusCode, "心跳应成功，实际响应：" + body);
            Assert.Contains("period", body, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            node.Delete();
        }
    }

    [Fact(DisplayName = "节点登录与心跳下发平台设置的默认语言")]
    public async Task LoginAndPing_SendsPlatformDefaultLanguage()
    {
        var set = StarServerSetting.Current;
        var old = set.DefaultLanguage;
        Node node = null;
        try
        {
            // 平台设置使用地区码，服务端归一化为词表语言后下发
            set.DefaultLanguage = "zh-TW";

            var (rs, node2) = await LoginNodeAsync("test_lang_" + Rand.NextString(6));
            node = node2;
            Assert.Equal("zh-Hant", rs.DefaultLanguage);

            var client = _server.CreateClient();
            client.DefaultRequestHeaders.Add("X-Token", rs.Token);

            var resp = await client.PostAsync("node/ping", JsonContent.Create(new { MachineName = node.MachineName, Macs = node.MACs }));
            var body = await resp.Content.ReadAsStringAsync();

            Assert.True(resp.IsSuccessStatusCode, "心跳应成功，实际响应：" + body);
            Assert.Contains("\"defaultLanguage\":\"zh-Hant\"", body, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            set.DefaultLanguage = old;
            node?.Delete();
        }
    }

    [Fact(DisplayName = "未携带令牌的节点心跳被拒绝")]
    public async Task Ping_WithoutToken_Rejected()
    {
        var client = _server.CreateClient();

        var resp = await client.PostAsync("node/ping", JsonContent.Create(new { MachineName = "no_token" }));
        var body = await resp.Content.ReadAsStringAsync();

        Assert.DoesNotContain("\"period\"", body, StringComparison.OrdinalIgnoreCase);
    }
}
