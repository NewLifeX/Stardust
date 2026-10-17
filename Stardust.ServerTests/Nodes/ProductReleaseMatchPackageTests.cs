using Stardust.Data.Nodes;
using Stardust.Models;
using Xunit;

namespace ServerTest.Nodes;

/// <summary>产品版本安装包匹配（ProductRelease.MatchPackage）测试。
/// 语义：先按产品编码与 OSKind/Architecture 过滤，再按运行时选择安装包；
/// .NET 保持主版本号匹配，原生 Agent 使用 go/rust/cpp，最后回退万能包。</summary>
public class ProductReleaseMatchPackageTests
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

        public ProductPackage AddPackage(String runtime, OSKind os = OSKind.Any, CpuArch arch = CpuArch.Any, String fileName = null)
        {
            var pkg = new ProductPackage
            {
                ReleaseId = Release.Id,
                OSKind = os,
                Architecture = arch,
                TargetRuntime = runtime,
                FileName = fileName ?? $"pkg-{Guid.NewGuid():n}.zip",
                Source = "http://127.0.0.1/files/pkg.zip",
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
            Release.Delete();
        }
    }

    private static Node NewNode(String productCode = "StarAgent", String runtime = "8.0.6", OSKinds osKind = OSKinds.Ubuntu, String arch = "Arm64") => new()
    {
        Code = "test-rel-" + Guid.NewGuid().ToString("n")[..8],
        Name = "test-rel",
        ProductCode = productCode,
        Runtime = runtime,
        OSKind = osKind,
        Architecture = arch,
        Version = "3.6.2026.0104",
        Enable = true,
    };

    private static void AssertPackage(ProductPackage expected, ProductPackage actual)
    {
        Assert.NotNull(actual);
        Assert.Equal(expected.Id, actual.Id);
    }

    /// <summary>.NET Agent 保持主版本号匹配，Runtime 为空时回退 net45 的“4”，未命中时回退万能包。</summary>
    [Fact]
    [Trait("Category", "Nodes")]
    public void DotNetRuntime_MajorVersionAndFallback()
    {
        using var scope = new ReleaseScope("StarAgent");
        var pkg8 = scope.AddPackage("8");
        var pkg4 = scope.AddPackage("4");
        var pkgAll = scope.AddPackage("*");

        AssertPackage(pkg8, scope.Release.MatchPackage(NewNode(runtime: "8.0.6")));
        AssertPackage(pkg4, scope.Release.MatchPackage(NewNode(runtime: "4.8.1")));
        AssertPackage(pkg4, scope.Release.MatchPackage(NewNode(runtime: null)));
        AssertPackage(pkgAll, scope.Release.MatchPackage(NewNode(runtime: "6.0.1")));
    }

    /// <summary>原生 Agent 按 go/rust/cpp 精确匹配 TargetRuntime。</summary>
    [Theory]
    [InlineData("StarAgent-Go", "go1.24.0", "go")]
    [InlineData("StarAgent-Rust", "rust1.83.0", "rust")]
    [InlineData("StarAgent-Cpp", "cpp20", "cpp")]
    [InlineData("StarAgent-Go", "GO1.24.0", "go")]
    [Trait("Category", "Nodes")]
    public void NativeRuntime_ExactMatch(String productCode, String runtime, String target)
    {
        using var scope = new ReleaseScope(productCode);
        var pkg = scope.AddPackage(target);
        scope.AddPackage("*");

        AssertPackage(pkg, scope.Release.MatchPackage(NewNode(productCode, runtime)));
    }

    /// <summary>原生 Agent 未命中运行时版本时回退万能包。</summary>
    [Fact]
    [Trait("Category", "Nodes")]
    public void NativeRuntime_FallbackToAnyPackage()
    {
        using var scope = new ReleaseScope("StarAgent-Go");
        scope.AddPackage("rust");
        var pkgAll = scope.AddPackage("*");

        AssertPackage(pkgAll, scope.Release.MatchPackage(NewNode("StarAgent-Go", "go1.24.0")));
    }

    /// <summary>OSKind/Architecture 先过滤，精确匹配优先于通用包。</summary>
    [Fact]
    [Trait("Category", "Nodes")]
    public void OSKindAndArchitecture_FilterAndPreferExact()
    {
        using var scope = new ReleaseScope("StarAgent-Go");
        var linuxArm = scope.AddPackage("go", OSKind.Linux, CpuArch.Arm64, "linux-arm64.zip");
        var linuxX64 = scope.AddPackage("go", OSKind.Linux, CpuArch.X64, "linux-x64.zip");
        var winX64 = scope.AddPackage("go", OSKind.Windows, CpuArch.X64, "win-x64.zip");
        var generic = scope.AddPackage("go");

        AssertPackage(linuxArm, scope.Release.MatchPackage(NewNode("StarAgent-Go", "go1.24.0", OSKinds.Ubuntu, "Arm64")));
        AssertPackage(linuxX64, scope.Release.MatchPackage(NewNode("StarAgent-Go", "go1.24.0", OSKinds.Ubuntu, "x64")));
        AssertPackage(winX64, scope.Release.MatchPackage(NewNode("StarAgent-Go", "go1.24.0", OSKinds.Win10, "X64")));

        // Alpine/OpenWrt 映射为 LinuxMusl，没有专用包时回退通用包
        AssertPackage(generic, scope.Release.MatchPackage(NewNode("StarAgent-Go", "go1.24.0", OSKinds.Alpine, "Arm64")));
        AssertPackage(generic, scope.Release.MatchPackage(NewNode("StarAgent-Go", "go1.24.0", OSKinds.OpenWrt, "Arm64")));
        // 未知架构只匹配通用包
        AssertPackage(generic, scope.Release.MatchPackage(NewNode("StarAgent-Go", "go1.24.0", OSKinds.Ubuntu, "mips")));
    }

    /// <summary>.NET Agent 在存在 OS/Arch 专用包时优先专用包，不破坏原有主版本匹配。</summary>
    [Fact]
    [Trait("Category", "Nodes")]
    public void DotNet_OSSpecificPackagePreferred()
    {
        using var scope = new ReleaseScope("StarAgent");
        var generic = scope.AddPackage("8");
        var linux = scope.AddPackage("8", OSKind.Linux, CpuArch.Arm64);

        AssertPackage(linux, scope.Release.MatchPackage(NewNode("StarAgent", "8.0.6", OSKinds.Ubuntu, "Arm64")));
        AssertPackage(generic, scope.Release.MatchPackage(NewNode("StarAgent", "8.0.6", OSKinds.Win10, "X64")));
    }

    /// <summary>产品编码不匹配时不得使用该版本的安装包；空产品编码的版本对所有节点通用。</summary>
    [Fact]
    [Trait("Category", "Nodes")]
    public void ProductCode_Filter()
    {
        using var scope = new ReleaseScope("StarAgent-Go");
        scope.AddPackage("*");

        Assert.Null(scope.Release.MatchPackage(NewNode("StarAgent", "8.0.6")));
        Assert.NotNull(scope.Release.MatchPackage(NewNode("StarAgent-Go", "go1.24.0")));

        using var genericRelease = new ReleaseScope(null);
        genericRelease.AddPackage("*");

        Assert.NotNull(genericRelease.Release.MatchPackage(NewNode("StarAgent", "8.0.6")));
        Assert.NotNull(genericRelease.Release.MatchPackage(NewNode("StarAgent-Go", "go1.24.0")));
    }

    /// <summary>停用的发布版本不参与匹配。</summary>
    [Fact]
    [Trait("Category", "Nodes")]
    public void DisabledRelease_NoMatch()
    {
        using var scope = new ReleaseScope("StarAgent");
        scope.AddPackage("8");
        scope.Release.Enable = false;

        Assert.Null(scope.Release.MatchPackage(NewNode(runtime: "8.0.6")));
    }
}
