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

/// <summary>原生 Agent 引导安装信息（InstallService.GetBootstrap）测试。
/// 语义：只有原生 Agent（StarAgent-Go/Rust/Cpp）才返回引导信息；
/// 完整版安装包按固定产品编码匹配，运行时包按节点系统与架构匹配，选包规则与安装页一致。</summary>
public class InstallServiceBootstrapTests
{
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

    /// <summary>测试用发布版本、安装包、运行时包与节点，结束后清理。</summary>
    private sealed class BootstrapScope : IDisposable
    {
        private readonly List<ProductPackage> _packages = [];
        private readonly List<DotNetPackage> _runtimes = [];

        public ProductRelease Release { get; }

        public Node Node { get; }

        public BootstrapScope(String nodeProductCode)
        {
            Release = new ProductRelease
            {
                Version = "9.9.9999." + Guid.NewGuid().ToString("n")[..8],
                ProductCode = "StarAgent",
                Enable = true,
                Force = false,
                Channel = NodeChannels.Release,
            };
            Release.Insert();

            Node = new Node
            {
                Code = "test-bs-" + Guid.NewGuid().ToString("n")[..8],
                Name = "test-bs",
                ProductCode = nodeProductCode,
                Runtime = "cpp20",
                OSKind = OSKinds.Ubuntu,
                Architecture = "Arm64",
                Version = "3.6.2026.0104",
                Enable = true,
            };
            Node.Insert();
        }

        public ProductPackage AddPackage(OSKind os, CpuArch arch, String runtime = "*")
        {
            var pkg = new ProductPackage
            {
                ReleaseId = Release.Id,
                OSKind = os,
                Architecture = arch,
                TargetRuntime = runtime,
                FileName = $"staragent-{Guid.NewGuid():n}.tar.gz",
                Source = "http://127.0.0.1/files/" + Guid.NewGuid().ToString("n") + ".tar.gz",
                Size = 2048,
                FileHash = "0123456789abcdef0123456789abcdef",
            };
            pkg.Insert();
            _packages.Add(pkg);

            return pkg;
        }

        public DotNetPackage AddRuntime(String version, OSKind os, CpuArch arch)
        {
            var pkg = new DotNetPackage
            {
                Version = version,
                Kind = "runtime",
                OSKind = os,
                Architecture = arch,
                FileName = $"dotnet-runtime-{version}-linux-arm64.tar.gz",
                Source = "https://example.com/dotnet/" + Guid.NewGuid().ToString("n") + ".tar.gz",
                Size = 1024,
                FileHash = "0123456789abcdef0123456789abcdef",
                Enable = true,
                // 清单与运行时匹配按通道过滤，必须显式指定 Release（枚举值不为 0）
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
            Node.Delete();
            Release.Delete();
        }
    }

    private static InstallService CreateService()
    {
        var provider = new CacheProvider { Cache = new MemoryCache(), InnerCache = new MemoryCache() };
        return new InstallService(StarServerSetting.Current, provider, new DownloadTokenService(provider));
    }

    /// <summary>原生 Agent 能同时拿到运行时包与完整版安装包，运行时版本取自安装包自身。</summary>
    [Fact]
    [Trait("Category", "Install")]
    public void NativeAgent_ReturnsRuntimeAndAgentPackages()
    {
        using var isolation = new CatalogIsolation();
        using var scope = new BootstrapScope("StarAgent-Cpp");

        var agentPkg = scope.AddPackage(OSKind.Linux, CpuArch.Arm64);
        var runtimePkg = scope.AddRuntime("10.0.9", OSKind.Linux, CpuArch.Arm64);

        var model = CreateService().GetBootstrap(scope.Node);

        Assert.NotNull(model);
        Assert.Equal(scope.Release.Version, model!.Version);
        Assert.Equal(scope.Node.Code, model.Code);

        Assert.NotNull(model.Agent);
        Assert.Equal(agentPkg.FileName, model.Agent!.FileName);
        Assert.Equal(agentPkg.Source, model.Agent.Source);
        Assert.Equal(scope.Release.Version, model.Agent.Version);

        Assert.NotNull(model.Runtime);
        Assert.Equal("10.0.9", model.Runtime!.Version);
        Assert.Equal("runtime", model.Runtime.Kind);
        Assert.Equal(runtimePkg.Source, model.Runtime.Source);
    }

    /// <summary>.NET 版 Agent 自身就是完整版，不返回引导信息。</summary>
    [Fact]
    [Trait("Category", "Install")]
    public void DotNetAgent_ReturnsNull()
    {
        using var isolation = new CatalogIsolation();
        using var scope = new BootstrapScope("StarAgent");

        scope.AddPackage(OSKind.Linux, CpuArch.Arm64);
        scope.AddRuntime("10.0.9", OSKind.Linux, CpuArch.Arm64);

        Assert.Null(CreateService().GetBootstrap(scope.Node));
    }

    /// <summary>服务端没有完整版发布包时返回空，不产生半套引导信息。</summary>
    [Fact]
    [Trait("Category", "Install")]
    public void MissingAgentPackage_ReturnsNull()
    {
        using var isolation = new CatalogIsolation();
        using var scope = new BootstrapScope("StarAgent-Go");

        scope.AddRuntime("10.0.9", OSKind.Linux, CpuArch.Arm64);

        Assert.Null(CreateService().GetBootstrap(scope.Node));
    }

    /// <summary>节点系统与架构优先精确匹配，通用包兜底。</summary>
    [Fact]
    [Trait("Category", "Install")]
    public void NativeAgent_PrefersExactOSAndArch()
    {
        using var isolation = new CatalogIsolation();
        using var scope = new BootstrapScope("StarAgent-Rust");

        scope.AddPackage(OSKind.Any, CpuArch.Any, "10");
        var exact = scope.AddPackage(OSKind.Linux, CpuArch.Arm64, "10");

        var model = CreateService().GetBootstrap(scope.Node);

        Assert.NotNull(model);
        Assert.Equal(exact.FileName, model!.Agent!.FileName);
    }
}
