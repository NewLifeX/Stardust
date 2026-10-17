using System.Runtime.CompilerServices;
using NewLife.Remoting.Models;
using Stardust.Data.Nodes;
using Stardust.Models;
using Stardust.Server.Services;
using Xunit;

namespace ServerTest.Nodes;

/// <summary>节点升级（NodeService.Upgrade）产品编码隔离测试。
/// 语义：升级只能使用通用版本或与节点产品编码一致的 ProductRelease，
/// .NET 包不得下发给 Go/Rust/C++ 原生 Agent。</summary>
public class NodeServiceUpgradeTests
{
    /// <summary>临时停用其它发布版本与升级策略，避免共享测试库中的遗留数据干扰断言。</summary>
    private sealed class UpgradeIsolation : IDisposable
    {
        private readonly List<ProductRelease> _releases = [];
        private readonly List<NodeVersion> _versions = [];

        public UpgradeIsolation()
        {
            foreach (var item in ProductRelease.FindAll().Where(e => e.Enable).ToList())
            {
                item.Enable = false;
                item.Update();
                _releases.Add(item);
            }

            foreach (var item in NodeVersion.FindAll().Where(e => e.Enable).ToList())
            {
                item.Enable = false;
                item.Update();
                _versions.Add(item);
            }
        }

        public void Dispose()
        {
            foreach (var item in _releases)
            {
                item.Enable = true;
                item.Update();
            }

            foreach (var item in _versions)
            {
                item.Enable = true;
                item.Update();
            }
        }
    }

    /// <summary>测试用发布版本、安装包与节点，结束后清理。</summary>
    private sealed class UpgradeScope : IDisposable
    {
        private readonly List<ProductPackage> _packages = [];

        public ProductRelease Release { get; }

        public Node Node { get; }

        public UpgradeScope(String releaseProductCode, String nodeProductCode, String runtime)
        {
            Release = new ProductRelease
            {
                Version = "9.9.9999." + Guid.NewGuid().ToString("n")[..8],
                ProductCode = releaseProductCode,
                Enable = true,
                Force = false,
                Channel = NodeChannels.Release,
            };
            Release.Insert();

            Node = new Node
            {
                Code = "test-up-" + Guid.NewGuid().ToString("n")[..8],
                Name = "test-up",
                ProductCode = nodeProductCode,
                Runtime = runtime,
                OSKind = OSKinds.Ubuntu,
                Architecture = "Arm64",
                Version = "3.6.2026.0104",
                Enable = true,
            };
            Node.Insert();
        }

        public ProductPackage AddPackage(String runtime)
        {
            var pkg = new ProductPackage
            {
                ReleaseId = Release.Id,
                TargetRuntime = runtime,
                FileName = $"pkg-{Guid.NewGuid():n}.zip",
                Source = "http://127.0.0.1/files/" + Guid.NewGuid().ToString("n") + ".zip",
                Size = 1024,
                FileHash = "0123456789abcdef0123456789abcdef",
            };
            pkg.Insert();
            _packages.Add(pkg);

            return pkg;
        }

        public void Dispose()
        {
            foreach (var pkg in _packages) pkg.Delete();
            Node.Delete();
            Release.Delete();
        }
    }

    private static NodeService CreateService() => (NodeService)RuntimeHelpers.GetUninitializedObject(typeof(NodeService));

    private static DeviceContext NewContext(Node node) => new() { Device = node, UserHost = "127.0.0.1" };

    /// <summary>只有 .NET 发布版本时，原生 Agent 不会拿到 .NET 安装包。</summary>
    [Fact]
    [Trait("Category", "Nodes")]
    public void NativeAgent_SkipsDotNetRelease()
    {
        using var isolation = new UpgradeIsolation();
        using var scope = new UpgradeScope("StarAgent", "StarAgent-Go", "go1.24.0");
        scope.AddPackage("8");

        var svc = CreateService();
        var info = svc.Upgrade(NewContext(scope.Node), "Release");

        Assert.Null(info);
        Assert.Null(scope.Node.LastVersion);
    }

    /// <summary>同时存在 .NET 与原生发布版本时，原生 Agent 命中自己的安装包。</summary>
    [Fact]
    [Trait("Category", "Nodes")]
    public void NativeAgent_GetsNativeRelease()
    {
        using var isolation = new UpgradeIsolation();
        using var dotnetScope = new UpgradeScope("StarAgent", "StarAgent-Go", "go1.24.0");
        dotnetScope.AddPackage("8");

        using var nativeScope = new UpgradeScope("StarAgent-Go", "StarAgent-Go", "go1.24.0");
        var pkg = nativeScope.AddPackage("go");

        var svc = CreateService();
        var info = svc.Upgrade(NewContext(nativeScope.Node), "Release");

        Assert.NotNull(info);
        Assert.Equal(nativeScope.Release.Version, info.Version);
        Assert.Equal(pkg.Source, info.Source);
        Assert.Equal(nativeScope.Release.Version, nativeScope.Node.LastVersion);
    }

    /// <summary>标准 .NET Agent 升级路径不受产品编码过滤影响。</summary>
    [Fact]
    [Trait("Category", "Nodes")]
    public void DotNetAgent_StillGetsRelease()
    {
        using var isolation = new UpgradeIsolation();
        using var scope = new UpgradeScope("StarAgent", "StarAgent", "8.0.6");
        var pkg = scope.AddPackage("8");

        var svc = CreateService();
        var info = svc.Upgrade(NewContext(scope.Node), "Release");

        Assert.NotNull(info);
        Assert.Equal(scope.Release.Version, info.Version);
        Assert.Equal(pkg.Source, info.Source);
        Assert.Equal(scope.Release.Version, scope.Node.LastVersion);
    }

    /// <summary>通用发布版本（产品编码为空）对 .NET 与原生 Agent 都生效。</summary>
    [Fact]
    [Trait("Category", "Nodes")]
    public void GenericRelease_AppliesToNativeAgent()
    {
        using var isolation = new UpgradeIsolation();
        using var scope = new UpgradeScope(null, "StarAgent-Rust", "rust1.83.0");
        var pkg = scope.AddPackage("rust");

        var svc = CreateService();
        var info = svc.Upgrade(NewContext(scope.Node), "Release");

        Assert.NotNull(info);
        Assert.Equal(scope.Release.Version, info.Version);
        Assert.Equal(pkg.Source, info.Source);
    }
}
