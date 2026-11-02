using System;
using System.Collections.Generic;
using System.Linq;
using NewLife;
using NewLife.Caching;
using Stardust.Data.Nodes;
using Stardust.Models;
using Stardust.Server;
using Stardust.Server.Services;
using Xunit;

namespace ServerTest.Install;

/// <summary>安装引导服务测试。语义：清单优先取后台维护的包、缺失回退公共镜像；
/// 下载地址统一指回本站并在访问时现场签发令牌；安装脚本携带服务端地址且不含会过期的令牌。</summary>
public class InstallServiceTests
{
    private const String BaseUri = "https://star.example.com:6600";

    /// <summary>临时停用其它 StarAgent 发布版本与运行时包，避免共享测试库遗留数据干扰断言。</summary>
    private sealed class CatalogIsolation : IDisposable
    {
        private readonly List<ProductRelease> _releases = [];
        private readonly List<DotNetPackage> _packages = [];

        public CatalogIsolation()
        {
            foreach (var item in ProductRelease.FindAll().Where(e => e.Enable && e.ProductCode.EqualIgnoreCase("StarAgent")).ToList())
            {
                item.Enable = false;
                item.Update();
                _releases.Add(item);
            }

            foreach (var item in DotNetPackage.FindAll().Where(e => e.Enable).ToList())
            {
                item.Enable = false;
                item.Update();
                _packages.Add(item);
            }
        }

        public void Dispose()
        {
            foreach (var item in _packages)
            {
                item.Enable = true;
                item.Update();
            }

            foreach (var item in _releases)
            {
                item.Enable = true;
                item.Update();
            }
        }
    }

    /// <summary>测试用发布版本、安装包与运行时包，结束后清理。</summary>
    private sealed class CatalogScope : IDisposable
    {
        private readonly List<ProductPackage> _packages = [];
        private readonly List<DotNetPackage> _runtimes = [];

        public ProductRelease Release { get; }

        public CatalogScope(String version, Boolean enable = true)
        {
            Release = new ProductRelease
            {
                Version = version,
                ProductCode = "StarAgent",
                Enable = enable,
                Force = false,
                Channel = NodeChannels.Release,
            };
            Release.Insert();
        }

        public ProductPackage AddPackage(OSKind os, CpuArch arch, String runtime = "*", String source = "/cube/file?id=123.zip")
        {
            var pkg = new ProductPackage
            {
                ReleaseId = Release.Id,
                OSKind = os,
                Architecture = arch,
                TargetRuntime = runtime,
                FileName = $"staragent-{Guid.NewGuid():n}.tar.gz",
                Source = source,
                Size = 2048,
                FileHash = "0123456789abcdef0123456789abcdef",
            };
            pkg.Insert();
            _packages.Add(pkg);

            return pkg;
        }

        public DotNetPackage AddRuntime(String version, OSKind os, CpuArch arch, String source = "/cube/file?id=124.tar.gz")
        {
            var pkg = new DotNetPackage
            {
                Version = version,
                Kind = "runtime",
                OSKind = os,
                Architecture = arch,
                FileName = $"dotnet-runtime-{version}-linux.tar.gz",
                Source = source,
                Size = 1024,
                FileHash = "0123456789abcdef0123456789abcdef",
                Enable = true,
                // GetValids 会按通道过滤，必须显式指定 Release（枚举值不为 0）
                Channel = NodeChannels.Release,
            };
            pkg.Insert();
            _runtimes.Add(pkg);

            return pkg;
        }

        public void Dispose()
        {
            foreach (var pkg in _packages) pkg.Delete();
            foreach (var pkg in _runtimes) pkg.Delete();
            Release.Delete();
        }
    }

    private static (InstallService Service, DownloadTokenService Tokens) CreateService()
    {
        // CacheProvider 默认取全局共享缓存，测试内换成独立实例，避免用例之间互相污染清单缓存
        var provider = new CacheProvider { Cache = new MemoryCache(), InnerCache = new MemoryCache() };
        var tokens = new DownloadTokenService(provider);
        return (new InstallService(StarServerSetting.Current, provider, tokens), tokens);
    }

    [Fact]
    [Trait("Category", "Install")]
    public void Manifest_ContainsReleasePackagesAndRuntime()
    {
        using var isolation = new CatalogIsolation();
        using var scope = new CatalogScope("9.9.9999." + Guid.NewGuid().ToString("n")[..8]);

        var agentPkg = scope.AddPackage(OSKind.Linux, CpuArch.Arm64, "10");
        scope.AddRuntime("10.0.9", OSKind.Linux, CpuArch.Arm64);

        var (service, _) = CreateService();
        var manifest = service.GetManifest(BaseUri);

        Assert.Equal(BaseUri, manifest.Server);

        var version = manifest.Versions.FirstOrDefault(e => e.Id == scope.Release.Id);
        Assert.NotNull(version);
        Assert.Equal(scope.Release.Version, version!.Version);

        var package = version.Packages.FirstOrDefault(e => e.Id == agentPkg.Id);
        Assert.NotNull(package);
        Assert.Equal("agent", package!.Kind);
        Assert.Equal("Linux", package.OS);
        Assert.Equal("Arm64", package.Arch);
        Assert.Equal("10", package.Runtime);
        Assert.Equal($"/install/download/agent/{agentPkg.Id}", package.Url);
        Assert.Equal("db", package.Origin);

        Assert.Contains(manifest.Runtimes, e => e.Version == "10.0.9" && e.Kind == "runtime");
    }

    /// <summary>后台没有任何安装包时，公共镜像兜底必须仍然可用，保证页面开箱不为空。</summary>
    [Fact]
    [Trait("Category", "Install")]
    public void Manifest_AlwaysContainsMirror()
    {
        using var isolation = new CatalogIsolation();

        var (service, _) = CreateService();
        var manifest = service.GetManifest(BaseUri);

        Assert.NotEmpty(manifest.Mirror);
        Assert.All(manifest.Mirror, e => Assert.Equal("mirror", e.Origin));
        Assert.All(manifest.Mirror, e => Assert.True(e.Url!.StartsWithIgnoreCase("http://", "https://"), e.Url));
        Assert.All(manifest.Mirror, e => Assert.Equal("agent", e.Kind));
    }

    /// <summary>下载地址指向本站下载入口，访问时才签发短时令牌。</summary>
    [Fact]
    [Trait("Category", "Install")]
    public void ResolveDownloadUrl_SignsShortToken()
    {
        using var isolation = new CatalogIsolation();
        using var scope = new CatalogScope("9.9.9999." + Guid.NewGuid().ToString("n")[..8]);
        var pkg = scope.AddPackage(OSKind.Linux, CpuArch.X64, "10", "/cube/file?id=123.zip");

        var (service, tokens) = CreateService();
        var url = service.ResolveDownloadUrl("agent", pkg.Id, BaseUri);

        Assert.NotNull(url);
        Assert.StartsWith($"{BaseUri}/cube/file?id=123.zip", url);
        Assert.Contains("token=", url);

        var token = url!.Split("token=")[1];
        Assert.True(tokens.Valid(123, token));
    }

    /// <summary>停用版本下的安装包不对外开放下载，避免匿名入口被用来枚举附件。</summary>
    [Fact]
    [Trait("Category", "Install")]
    public void ResolveDownloadUrl_DisabledRelease_ReturnsNull()
    {
        using var isolation = new CatalogIsolation();
        using var scope = new CatalogScope("9.9.9999." + Guid.NewGuid().ToString("n")[..8], enable: false);
        var pkg = scope.AddPackage(OSKind.Linux, CpuArch.X64, "10");

        var (service, _) = CreateService();

        Assert.Null(service.ResolveDownloadUrl("agent", pkg.Id, BaseUri));
        Assert.Null(service.ResolveDownloadUrl("agent", 0, BaseUri));
        Assert.Null(service.ResolveDownloadUrl("unknown", pkg.Id, BaseUri));
    }

    [Fact]
    [Trait("Category", "Install")]
    public void BuildScript_ContainsServerAndPackageUrl()
    {
        using var isolation = new CatalogIsolation();
        using var scope = new CatalogScope("9.9.9999." + Guid.NewGuid().ToString("n")[..8]);
        var pkg = scope.AddPackage(OSKind.Linux, CpuArch.X64, "10");
        var runtime = scope.AddRuntime("10.0.9", OSKind.Linux, CpuArch.X64);

        var (service, _) = CreateService();
        var script = service.BuildScript("linux", "x64", "10", null, BaseUri);

        Assert.NotNull(script);
        Assert.Contains($"SERVER=\"{BaseUri}\"", script);
        Assert.Contains($"{BaseUri}/install/download/agent/{pkg.Id}", script);
        Assert.Contains($"{BaseUri}/install/download/runtime/{runtime.Id}", script);
        Assert.Contains("StarAgent.dll -install -server", script);

        // 脚本里不允许出现会过期的签名地址
        Assert.DoesNotContain("token=", script);
    }

    /// <summary>服务端未维护安装包时，脚本退回公共镜像，仍然可安装。</summary>
    [Fact]
    [Trait("Category", "Install")]
    public void BuildScript_FallsBackToMirror()
    {
        using var isolation = new CatalogIsolation();

        var (service, _) = CreateService();
        var script = service.BuildScript("linux", "x64", null, null, BaseUri);

        Assert.NotNull(script);
        Assert.Contains("staragent10.tar.gz", script);
        Assert.Contains("StarAgent.dll -install -server", script);
    }

    /// <summary>未指定架构时下发自检脚本，架构由目标机识别，页面无需选择。</summary>
    [Fact]
    [Trait("Category", "Install")]
    public void BuildScript_WithoutArch_ReturnsBootstrap()
    {
        var (service, _) = CreateService();

        var script = service.BuildScript("linux", null, null, null, BaseUri);

        Assert.NotNull(script);
        Assert.Contains("uname -m", script);
        Assert.Contains("install.sh?os=linux&arch=", script);
    }

    [Fact]
    [Trait("Category", "Install")]
    public void BuildScript_RejectsInvalidServer()
    {
        var (service, _) = CreateService();

        Assert.Throws<ArgumentException>(() => service.BuildScript("linux", "x64", null, null, "https://evil.com'\nrm -rf /"));
        Assert.Throws<ArgumentException>(() => service.BuildScript("linux", "x64", null, null, ""));
    }

    /// <summary>无法识别的系统不生成脚本，避免把错误的包发给客户端。</summary>
    [Fact]
    [Trait("Category", "Install")]
    public void BuildScript_UnknownOS_ReturnsNull()
    {
        var (service, _) = CreateService();

        Assert.Null(service.BuildScript("plan9", "x64", null, null, BaseUri));
    }

    /// <summary>安装页面必须能从资源中读取到，避免根路径返回空。</summary>
    [Fact]
    [Trait("Category", "Install")]
    public void GetPage_ReturnsHtml()
    {
        var (service, _) = CreateService();
        var html = service.GetPage();

        Assert.NotNull(html);
        Assert.Contains("StarAgent", html);
    }
}
