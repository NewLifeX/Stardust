using System;
using System.Net.Http;
using System.Threading.Tasks;
using NewLife.Remoting;
using NewLife.Remoting.Models;
using NewLife.Security;
using Stardust.Data.Deployment;
using Stardust.Data.Nodes;
using Stardust.Models;
using Xunit;

namespace ServerTest.Controllers;

/// <summary>应用包节点 API 测试。应用包即发布中心部署集，覆盖鉴权、搜索、最新版本与下载令牌（PUB-25）</summary>
[Collection(StarTestServerCollection.Name)]
public class PackageControllerTests : StarTestBase
{
    public PackageControllerTests(StarTestServer fixture) : base(fixture) { }

    private async Task<(String token, Node node)> LoginAndGetNodeAsync()
    {
        var client = _server.CreateClient();
        var rs = await client.PostAsync<LoginResponse>("node/login", new
        {
            code = "",
            node = new
            {
                MachineName = "test_package_ctl",
                macs = "xxyyzz" + Guid.NewGuid().ToString("N")[..6],
            },
        });
        Assert.NotNull(rs?.Token);
        var node = Node.FindByCode(rs?.Code);
        Assert.NotNull(node);
        return (rs.Token, node);
    }

    private static AppDeploy CreatePackage()
    {
        var package = new AppDeploy
        {
            Name = "pkg_" + Rand.NextString(8),
            Category = "test",
            Enable = true,
        };
        package.Insert();

        return package;
    }

    private static AppDeployVersion CreateVersion(AppDeploy package)
    {
        var version = new AppDeployVersion
        {
            DeployId = package.Id,
            Version = "1.0",
            Enable = true,
            OS = OSKind.Any,
            Arch = CpuArch.Any,
            Url = "/cube/file?id=1.zip",
            Hash = "0f343b0931126a20f133d67c2b018a3b",
            Size = 1024,
        };
        version.Insert();
        package.Version = "1.0";
        package.Update();

        return version;
    }

    private static void Cleanup(AppDeploy package)
    {
        foreach (var item in AppDeployVersion.FindAllByDeployId(package.Id, 1000)) item.Delete();
        package.Delete();
    }

    [Fact(DisplayName = "无 Token 搜索应用包返回 401")]
    public async Task Search_NoToken_Returns401()
    {
        var client = _server.CreateClient();

        var response = await client.GetAsync("/Package/Search?key=test");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Contains("401", body);
    }

    [Fact(DisplayName = "带 Token 搜索应用包")]
    public async Task Search_WithToken_ReturnsPackage()
    {
        var (token, _) = await LoginAndGetNodeAsync();
        var package = CreatePackage();
        CreateVersion(package);

        try
        {
            var client = _server.CreateClient();
            client.DefaultRequestHeaders.Add("X-Token", token);

            var response = await client.GetAsync($"/Package/Search?key={package.Name}");
            var body = await response.Content.ReadAsStringAsync();

            Assert.Contains(package.FullName, body);
            Assert.Contains("1.0", body);
        }
        finally
        {
            Cleanup(package);
        }
    }

    [Fact(DisplayName = "获取最新版本返回下载地址与令牌")]
    public async Task GetLatest_WithToken_ReturnsUrl()
    {
        var (token, _) = await LoginAndGetNodeAsync();
        var package = CreatePackage();
        CreateVersion(package);

        try
        {
            var client = _server.CreateClient();
            client.DefaultRequestHeaders.Add("X-Token", token);

            var response = await client.GetAsync($"/Package/GetLatest?name={package.FullName}&os=0&arch=0");
            var body = await response.Content.ReadAsStringAsync();

            Assert.Contains("\"version\":\"1.0\"", body);
            Assert.Contains("token=", body);
        }
        finally
        {
            Cleanup(package);
        }
    }
}