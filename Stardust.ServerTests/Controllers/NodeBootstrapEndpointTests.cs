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
using Stardust.Models;
using Stardust.Server;
using Xunit;

namespace ServerTest.Controllers;

/// <summary>引导安装接口（node/bootstrap）端到端测试。
/// 语义：只有原生 Agent 返回引导信息；安装包来源补全为绝对地址并附加下载令牌；
/// .NET 版 Agent 自身即完整版，接口返回空。</summary>
[Collection(StarTestServerCollection.Name)]
public class NodeBootstrapEndpointTests : StarTestBase
{
    public NodeBootstrapEndpointTests(StarTestServer fixture) : base(fixture) { }

    /// <summary>测试用发布版本、安装包与运行时包，结束后清理。</summary>
    private sealed class BootstrapScope : IDisposable
    {
        private readonly ProductRelease _release;
        private readonly ProductPackage _package;
        private readonly DotNetPackage _runtime;

        public BootstrapScope(String agentSource, String runtimeSource)
        {
            _release = new ProductRelease
            {
                Version = "9.9.9999." + Guid.NewGuid().ToString("n")[..8],
                ProductCode = "StarAgent",
                Enable = true,
                Force = false,
                Channel = NodeChannels.Release,
            };
            _release.Insert();

            _package = new ProductPackage
            {
                ReleaseId = _release.Id,
                OSKind = OSKind.Linux,
                Architecture = CpuArch.X64,
                TargetRuntime = "*",
                FileName = "staragent-endpoint.tar.gz",
                Source = agentSource,
                Size = 2048,
                FileHash = "0123456789abcdef0123456789abcdef",
            };
            _package.Insert();

            _runtime = new DotNetPackage
            {
                Version = "9.9." + Guid.NewGuid().ToString("n")[..8],
                Kind = "runtime",
                OSKind = OSKind.Linux,
                Architecture = CpuArch.X64,
                FileName = "dotnet-runtime-endpoint.tar.gz",
                Source = runtimeSource,
                Size = 1024,
                FileHash = "0123456789abcdef0123456789abcdef",
                Enable = true,
                Channel = NodeChannels.Release,
            };
            _runtime.Insert();
        }

        public void Dispose()
        {
            _runtime.Delete();
            _package.Delete();
            _release.Delete();
        }
    }

    private async Task<(String token, Node node)> LoginAsync(String productCode)
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
                Compile = "2026-11-02 08:00:00",
                OSName = "Ubuntu",
                OSVersion = "24.04",
                Architecture = "X64",
                MachineName = "bootstrap_" + Rand.NextString(6),
                UserName = "root",
                Macs = macs,
                Runtime = productCode == "StarAgent-Cpp" ? "cpp17" : "10.0.0",
                Framework = productCode == "StarAgent-Cpp" ? "cpp17" : "10.0.0",
                TimeZone = "Asia/Shanghai",
                Time = "2026-11-02 10:00:00",
                Plugins = new[] { "agent:cpp", "cap:bootstrap=true", "os:linux" },
            },
        };

        var resp = await client.PostAsync("node/login", JsonContent.Create(body));
        var text = await resp.Content.ReadAsStringAsync();
        Assert.True(resp.IsSuccessStatusCode, "登录应成功：" + text);

        var rs = ParseData<LoginResponse>(text);
        Assert.NotNull(rs);
        Assert.False(rs!.Token.IsNullOrEmpty(), "登录响应缺少令牌");

        var node = Node.FindByCode(rs.Code);
        Assert.NotNull(node);

        return (rs.Token!, node!);
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

    /// <summary>静态目录（/files）只补全地址，不加签；加签只针对 /cube/file 附件。</summary>
    [Fact(DisplayName = "原生 Agent 取到绝对地址的引导安装包")]
    public async Task NativeAgent_GetsAbsolutePackages()
    {
        using var scope = new BootstrapScope("/files/staragent-endpoint.tar.gz", "/files/dotnet-runtime-endpoint.tar.gz");
        var (token, node) = await LoginAsync("StarAgent-Cpp");
        try
        {
            var client = _server.CreateClient();
            client.DefaultRequestHeaders.Add("Authorization", "Bearer " + token);

            var resp = await client.PostAsync("node/bootstrap", JsonContent.Create(new { }));
            var text = await resp.Content.ReadAsStringAsync();
            Assert.True(resp.IsSuccessStatusCode, "引导安装接口应成功：" + text);

            var model = ParseData<BootstrapModel>(text);
            Assert.NotNull(model);
            Assert.Equal(node.Code, model!.Code);

            var agent = model.Agent;
            Assert.NotNull(agent);
            Assert.Equal("staragent-endpoint.tar.gz", agent!.FileName);
            Assert.NotNull(agent.Source);
            Assert.StartsWith("http", agent.Source, StringComparison.OrdinalIgnoreCase);
            // 静态目录靠 IP 白名单，不追加下载令牌
            Assert.DoesNotContain("token=", agent.Source);

            var runtime = model.Runtime;
            Assert.NotNull(runtime);
            Assert.Equal("runtime", runtime!.Kind);
            Assert.NotNull(runtime.Source);
            Assert.StartsWith("http", runtime.Source, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("token=", runtime.Source);
        }
        finally
        {
            node.Delete();
        }
    }

    /// <summary>Cube 附件下载地址需要短时令牌，接口下发时必须现场签发。</summary>
    [Fact(DisplayName = "Cube 附件下载地址附带短时令牌")]
    public async Task AttachmentSource_GetsToken()
    {
        using var scope = new BootstrapScope("/cube/file?id=123.zip", "/cube/file?id=124.tar.gz");
        var (token, node) = await LoginAsync("StarAgent-Go");
        try
        {
            var client = _server.CreateClient();
            client.DefaultRequestHeaders.Add("Authorization", "Bearer " + token);

            var resp = await client.PostAsync("node/bootstrap", JsonContent.Create(new { }));
            var text = await resp.Content.ReadAsStringAsync();
            Assert.True(resp.IsSuccessStatusCode, "引导安装接口应成功：" + text);

            var model = ParseData<BootstrapModel>(text);
            Assert.NotNull(model);
            Assert.NotNull(model!.Agent);
            Assert.StartsWith("http", model.Agent.Source, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("token=", model.Agent.Source);
            Assert.NotNull(model.Runtime);
            Assert.Contains("token=", model.Runtime!.Source);
        }
        finally
        {
            node.Delete();
        }
    }

    /// <summary>.NET 版 Agent 自身即完整版，接口返回空。</summary>
    [Fact(DisplayName = "非原生 Agent 不返回引导信息")]
    public async Task DotNetAgent_GetsNull()
    {
        using var scope = new BootstrapScope("/files/staragent-endpoint.tar.gz", "/files/dotnet-runtime-endpoint.tar.gz");
        var (token, node) = await LoginAsync("StarAgent");
        try
        {
            var client = _server.CreateClient();
            client.DefaultRequestHeaders.Add("Authorization", "Bearer " + token);

            var resp = await client.PostAsync("node/bootstrap", JsonContent.Create(new { }));
            var text = await resp.Content.ReadAsStringAsync();

            Assert.True(resp.IsSuccessStatusCode, "接口应成功返回空：" + text);
            Assert.True(text.IsNullOrEmpty() || text.Trim() == "null" || !text.Contains("Agent", StringComparison.OrdinalIgnoreCase),
                "非原生 Agent 不应返回安装包：" + text);
        }
        finally
        {
            node.Delete();
        }
    }
}
