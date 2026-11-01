using Stardust.Data.Nodes;
using Stardust.Models;
using Xunit;

namespace ServerTest.Nodes;

/// <summary>按指定产品编码匹配安装包（ProductRelease.MatchProduct）测试。
/// 语义：忽略节点自身产品编码，用于原生 Agent 引导安装完整版 StarAgent；
/// 先按 OSKind/Architecture 过滤（0 表示通用），精确匹配优先。</summary>
public class ProductReleaseMatchProductTests
{
    /// <summary>测试用发布版本与安装包，结束后清理。</summary>
    private sealed class ReleaseScope : IDisposable
    {
        private readonly List<ProductPackage> _packages = [];

        public ProductRelease Release { get; }

        public ReleaseScope(String productCode)
        {
            Release = new ProductRelease
            {
                Version = "9.9.9999." + Guid.NewGuid().ToString("n")[..8],
                ProductCode = productCode,
                Enable = true,
                Force = false,
                Channel = NodeChannels.Release,
            };
            Release.Insert();
        }

        public ProductPackage AddPackage(OSKind os, CpuArch arch, String runtime = "*")
        {
            var pkg = new ProductPackage
            {
                ReleaseId = Release.Id,
                OSKind = os,
                Architecture = arch,
                TargetRuntime = runtime,
                FileName = $"pkg-{Guid.NewGuid():n}.zip",
                Source = "http://127.0.0.1/files/pkg.zip",
                Size = 2048,
                FileHash = "0123456789abcdef0123456789abcdef",
            };
            pkg.Insert();
            _packages.Add(pkg);

            return pkg;
        }

        public void Dispose()
        {
            foreach (var pkg in _packages) pkg.Delete();
            Release.Delete();
        }
    }

    private static Node NewNode(String productCode, OSKinds osKind = OSKinds.Ubuntu, String arch = "Arm64") => new()
    {
        Code = "test-bs-" + Guid.NewGuid().ToString("n")[..8],
        Name = "test-bs",
        ProductCode = productCode,
        Runtime = "cpp20",
        OSKind = osKind,
        Architecture = arch,
        Version = "3.6.2026.0104",
        Enable = true,
    };

    /// <summary>节点是原生 Agent 时，仍能匹配到 .NET 版 StarAgent 的安装包。</summary>
    [Fact]
    [Trait("Category", "Nodes")]
    public void MatchProduct_IgnoresNodeProductCode()
    {
        using var scope = new ReleaseScope("StarAgent");
        var pkg = scope.AddPackage(OSKind.Linux, CpuArch.Arm64);

        var (release, package) = ProductRelease.MatchProduct(NewNode("StarAgent-Cpp"), "StarAgent");

        Assert.NotNull(release);
        Assert.Equal(scope.Release.Id, release.Id);
        Assert.NotNull(package);
        Assert.Equal(pkg.Id, package.Id);
    }

    /// <summary>精确匹配系统与架构优先于通用包。</summary>
    [Fact]
    [Trait("Category", "Nodes")]
    public void MatchProduct_PrefersExactOSAndArch()
    {
        using var scope = new ReleaseScope("StarAgent");
        scope.AddPackage(OSKind.Any, CpuArch.Any);
        scope.AddPackage(OSKind.Windows, CpuArch.X64);
        var exact = scope.AddPackage(OSKind.Linux, CpuArch.Arm64);

        var (_, package) = ProductRelease.MatchProduct(NewNode("StarAgent-Go"), "StarAgent");

        Assert.NotNull(package);
        Assert.Equal(exact.Id, package.Id);
    }

    /// <summary>目标产品没有可用发行版本时返回空。</summary>
    [Fact]
    [Trait("Category", "Nodes")]
    public void MatchProduct_NoRelease_ReturnsNull()
    {
        var (release, package) = ProductRelease.MatchProduct(NewNode("StarAgent-Cpp"), "NoSuchProduct-" + Guid.NewGuid().ToString("n")[..8]);

        Assert.Null(release);
        Assert.Null(package);
    }
}
