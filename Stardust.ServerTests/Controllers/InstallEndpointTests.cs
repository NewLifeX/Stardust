using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using NewLife;
using Stardust.Data.Nodes;
using Stardust.Models;
using Xunit;

namespace ServerTest.Controllers;

/// <summary>安装引导接口（/、/install、/install.sh、/install/download）端到端测试。
/// 语义：根路径返回安装页面；清单匿名可取且带服务端地址；脚本可直接执行；
/// 下载接口只放行启用版本下的安装包并现场签发令牌。</summary>
[Collection(StarTestServerCollection.Name)]
public class InstallEndpointTests : StarTestBase
{
    public InstallEndpointTests(StarTestServer fixture) : base(fixture) { }

    private sealed class PackageScope : IDisposable
    {
        private readonly ProductRelease _release;
        private readonly ProductPackage _package;

        public ProductPackage Package => _package;

        public PackageScope()
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
                TargetRuntime = "10",
                FileName = "staragent-install.tar.gz",
                Source = "/cube/file?id=456.tar.gz",
                Size = 2048,
                FileHash = "0123456789abcdef0123456789abcdef",
            };
            _package.Insert();
        }

        public void Dispose()
        {
            _package.Delete();
            _release.Delete();
        }
    }

    [Fact]
    [Trait("Category", "Install")]
    public async Task Summary_ReturnsPlatformCounts()
    {
        var client = _server.CreateClient();

        var text = await client.GetStringAsync("/home/summary");
        using var doc = JsonDocument.Parse(text);
        var root = doc.RootElement;

        Assert.False(root.GetProperty("server").GetString().IsNullOrEmpty());
        Assert.False(root.GetProperty("version").GetString().IsNullOrEmpty());
        Assert.False(root.GetProperty("mode").GetString().IsNullOrEmpty());
        Assert.True(root.GetProperty("nodeTotal").GetInt32() >= 0);
        Assert.True(root.GetProperty("appTotal").GetInt32() >= 0);
        Assert.InRange(root.GetProperty("errorRate").GetDouble(), 0, 1);
        Assert.Equal(7, root.GetProperty("trend").GetArrayLength());
    }

    [Fact]
    [Trait("Category", "Install")]
    public async Task Leaf_ReturnsPngIcon()
    {
        var client = _server.CreateClient();

        var resp = await client.GetAsync("/install/leaf.png");
        Assert.True(resp.IsSuccessStatusCode, "品牌图标应可访问");
        Assert.Equal("image/png", resp.Content.Headers.ContentType?.MediaType);

        var bytes = await resp.Content.ReadAsByteArrayAsync();
        Assert.True(bytes.Length > 1000, "图标应嵌入程序集并可读取");
    }

    [Fact]
    [Trait("Category", "Install")]
    public async Task Root_ReturnsInstallPage()
    {
        var client = _server.CreateClient();

        var resp = await client.GetAsync("/");
        Assert.True(resp.IsSuccessStatusCode, "根路径应返回安装页面");
        Assert.Contains("text/html", resp.Content.Headers.ContentType + "");

        var html = await resp.Content.ReadAsStringAsync();
        Assert.Contains("StarAgent", html);
        Assert.Contains("install", html);
    }

    [Fact]
    [Trait("Category", "Install")]
    public async Task Manifest_ReturnsServerAndMirror()
    {
        var client = _server.CreateClient();

        var text = await client.GetStringAsync("/install?format=json");
        using var doc = JsonDocument.Parse(text);
        var root = doc.RootElement;

        var server = root.GetProperty("server").GetString();
        Assert.False(server.IsNullOrEmpty());
        Assert.True(root.GetProperty("mirror").GetArrayLength() > 0);
    }

    [Fact]
    [Trait("Category", "Install")]
    public async Task ShellScript_ReturnsExecutableScript()
    {
        var client = _server.CreateClient();

        var script = await client.GetStringAsync("/install.sh?os=linux&arch=x64");

        Assert.Contains("StarAgent.dll -install -server", script);
        Assert.DoesNotContain("token=", script);
    }

    /// <summary>页面不选架构时，下发的是自检脚本，由目标机识别架构后再拉真正安装脚本。</summary>
    [Fact]
    [Trait("Category", "Install")]
    public async Task ShellScript_WithoutArch_ReturnsBootstrap()
    {
        var client = _server.CreateClient();

        var script = await client.GetStringAsync("/install.sh?os=linux");

        Assert.Contains("uname -m", script);
        Assert.Contains("install.sh?os=linux&arch=", script);
    }

    [Fact]
    [Trait("Category", "Install")]
    public async Task Download_EnabledPackage_RedirectsWithToken()
    {
        using var scope = new PackageScope();
        var client = _server.CreateClient();

        var resp = await client.GetAsync($"/install/download/agent/{scope.Package.Id}");

        Assert.Equal(HttpStatusCode.Redirect, resp.StatusCode);
        var location = resp.Headers.Location + "";
        Assert.Contains("id=456.tar.gz", location);
        Assert.Contains("token=", location);
    }

    [Fact]
    [Trait("Category", "Install")]
    public async Task Download_UnknownPackage_ReturnsNotFound()
    {
        var client = _server.CreateClient();

        var resp = await client.GetAsync("/install/download/agent/999999");

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }
}
