using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using NewLife;
using NewLife.Remoting.Models;
using NewLife.Security;
using Stardust.Data.Nodes;
using Stardust.Server;
using Xunit;

namespace ServerTest.Controllers;

/// <summary>原生 Agent 协议契约测试。使用与 StarAgent.Native/spec/protocol.md 一致的 PascalCase 报文验证 node/login、node/ping 与命令回复。</summary>
public class NativeAgentProtocolTests
{
    private readonly TestServer _server;

    public NativeAgentProtocolTests()
    {
#pragma warning disable CS0618, ASPDEPR008
        _server = new TestServer(WebHost.CreateDefaultBuilder().UseStartup<Startup>());
#pragma warning restore CS0618, ASPDEPR008
    }

    private async Task<(String token, Node node)> LoginNativeAsync(String productCode)
    {
        var client = _server.CreateClient();
        var macs = "AA-BB-CC-" + Rand.NextBytes(3).ToHex("-");
        var body = new
        {
            Code = "",
            Secret = "",
            ProductCode = productCode,
            ClientId = "127.0.0.1@1",
            Node = new
            {
                Version = "1.0.2026.1017",
                Compile = "2026-10-17 08:00:00",
                OSName = "Ubuntu",
                OSVersion = "24.04",
                Architecture = "X64",
                MachineName = "native_" + Rand.NextString(6),
                UserName = "root",
                Macs = macs,
                Runtime = productCode == "StarAgent-Go" ? "go1.24.0" : "rust1.99.0",
                Framework = productCode == "StarAgent-Go" ? "go1.24.0" : "rust1.99.0",
                TimeZone = "Asia/Shanghai",
                Time = "2026-10-17 10:00:00",
                Plugins = new[] { "agent:go", "cap:exec:false", "cap:upgrade=false", "os:linux" },
            },
        };

        var resp = await client.PostAsync("node/login", JsonContent.Create(body));
        var text = await resp.Content.ReadAsStringAsync();
        Assert.True(resp.IsSuccessStatusCode, "原生 Agent 登录应成功：" + text);

        var rs = ParseData<LoginResponse>(text);
        Assert.NotNull(rs);
        Assert.False(rs!.Token.IsNullOrEmpty(), "登录响应缺少令牌");
        Assert.False(rs.Code.IsNullOrEmpty(), "首次登录应下发节点编码");
        Assert.False(rs.Secret.IsNullOrEmpty(), "首次登录应下发节点密钥");

        var node = Node.FindByCode(rs.Code);
        Assert.NotNull(node);
        Assert.Equal(productCode, node!.ProductCode);

        return (rs.Token!, node);
    }

    /// <summary>解析裸对象或 {code,data,msg} 包装的响应。</summary>
    private static T? ParseData<T>(String text)
    {
        using var doc = JsonDocument.Parse(text);
        var root = doc.RootElement;
        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("data", out var data))
            return data.Deserialize<T>(new JsonSerializerOptions(JsonSerializerDefaults.Web));

        return root.Deserialize<T>(new JsonSerializerOptions(JsonSerializerDefaults.Web));
    }

    [Theory]
    [InlineData("StarAgent-Go")]
    [InlineData("StarAgent-Rust")]
    public async Task NativeLogin_WithPascalCase_Works(String productCode)
    {
        var (token, node) = await LoginNativeAsync(productCode);
        try
        {
            var client = _server.CreateClient();
            client.DefaultRequestHeaders.Add("Authorization", "Bearer " + token);

            var ping = new
            {
                Time = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                IP = "10.0.0.8",
                Gateway = "10.0.0.1",
                Dns = "223.5.5.5",
                Macs = node.MACs,
                ProcessCount = 10,
                Memory = 1024UL,
                AvailableMemory = 512UL,
                CpuRate = 12.5,
                Uptime = 100,
                SystemLoad = 0.5,
            };

            var resp = await client.PostAsync("node/ping", JsonContent.Create(ping));
            var text = await resp.Content.ReadAsStringAsync();
            Assert.True(resp.IsSuccessStatusCode, "原生 Agent 心跳应成功：" + text);
            Assert.Contains("\"period\"", text, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            node.Delete();
        }
    }
}
