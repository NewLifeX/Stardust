using Stardust.Data.Nodes;
using Stardust.Models;
using Xunit;

namespace ServerTest.Nodes;

/// <summary>手工推送安装包解析（DotNetPackage.ResolveForPush）测试。
/// 语义：版本留空或auto时按节点自动匹配最新的启用安装包，随后执行操作系统与GLIBC兼容性检查；
/// 检查不通过返回空并给出原因。该逻辑只服务手工推送，自动推送走 NodeVersion 策略。</summary>
public class DotNetPackagePushTests
{
    /// <summary>构造测试节点。只读取字段参与匹配与检查，不入库</summary>
    private static Node NewNode(String os, String clib = null) => new()
    {
        Code = "test-push-" + Guid.NewGuid().ToString("n")[..8],
        Name = "test-push",
        OS = os,
        OSKind = OSKinds.Ubuntu,
        Architecture = "Arm64",
        CLibVersion = clib,
        Enable = true,
    };

    /// <summary>确保安装包表存在启用的 aspnet/Linux/Arm64 指定版本安装包，返回是否为本测试新建</summary>
    private static (DotNetPackage package, Boolean created) EnsurePackage(String version)
    {
        var pkg = DotNetPackage.FindAll(DotNetPackage._.Kind == "aspnet" & DotNetPackage._.Version == version)
            .FirstOrDefault(e => e.Enable && e.OSKind == OSKind.Linux && e.Architecture == CpuArch.Arm64);
        if (pkg != null) return (pkg, false);

        pkg = new DotNetPackage
        {
            Version = version,
            Kind = "aspnet",
            OSKind = OSKind.Linux,
            Architecture = CpuArch.Arm64,
            FileName = $"aspnetcore-runtime-{version}-linux-arm64.tar.gz",
            Source = $"https://builds.dotnet.microsoft.com/dotnet/aspnetcore/Runtime/{version}/aspnetcore-runtime-{version}-linux-arm64.tar.gz",
            Enable = true,
            Channel = NodeChannels.Release,
        };
        pkg.Insert();
        return (pkg, true);
    }

    /// <summary>版本留空或auto：按节点 OS/Arch/Kind 匹配最新的启用安装包</summary>
    [Fact]
    [Trait("Category", "Nodes")]
    public void AutoVersion_MatchNewestEnabledPackage()
    {
        // 插入一个版本号极高的临时包，确保它是当前最新的启用包
        var (marker, created) = EnsurePackage("99.0.1");
        try
        {
            var node = NewNode("Ubuntu 22.04.3 LTS", "glibc 2.35");
            var pkg = DotNetPackage.ResolveForPush(node, "auto", "auto", out var reason);

            Assert.Null(reason);
            Assert.NotNull(pkg);
            Assert.Equal("99.0.1", pkg.Version);
            Assert.Equal("aspnet", pkg.Kind);
        }
        finally
        {
            if (created) marker.Delete();
        }
    }

    /// <summary>操作系统过旧（Ubuntu 18 / 20.04 无法安装 .NET 10）：跳过并给出原因</summary>
    [Fact]
    [Trait("Category", "Nodes")]
    public void OSIncompatible_Skip()
    {
        var (pkg10, created) = EnsurePackage("10.0.12");
        try
        {
            // 三段版本号（真实 PRETTY_NAME）与两段版本号都要能识别
            foreach (var os in new[] { "Ubuntu 18.04.5 LTS", "Ubuntu 18.04 LTS", "Ubuntu 20.04.6 LTS" })
            {
                var node = NewNode(os, "glibc 2.35");
                var pkg = DotNetPackage.ResolveForPush(node, "10.0.12", "aspnet", out var reason);

                Assert.Null(pkg);
                Assert.Contains("不兼容", reason);
            }
        }
        finally
        {
            if (created) pkg10.Delete();
        }
    }

    /// <summary>GLIBC 版本不足（.NET 10 要求 2.27）：跳过并给出原因</summary>
    [Fact]
    [Trait("Category", "Nodes")]
    public void GLibcTooOld_Skip()
    {
        var (pkg10, created) = EnsurePackage("10.0.12");
        try
        {
            var node = NewNode("Ubuntu 22.04.3 LTS", "glibc 2.17");
            var pkg = DotNetPackage.ResolveForPush(node, "10.0.12", "aspnet", out var reason);

            Assert.Null(pkg);
            Assert.Contains("GLIBC", reason);
        }
        finally
        {
            if (created) pkg10.Delete();
        }
    }

    /// <summary>指定版本且检查通过：返回对应安装包</summary>
    [Fact]
    [Trait("Category", "Nodes")]
    public void ExplicitVersion_ChecksPass()
    {
        var (pkg10, created) = EnsurePackage("10.0.12");
        try
        {
            var node = NewNode("Ubuntu 22.04.3 LTS", "glibc 2.35");
            var pkg = DotNetPackage.ResolveForPush(node, "10.0.12", "aspnet", out var reason);

            Assert.Null(reason);
            Assert.NotNull(pkg);
            Assert.Equal("10.0.12", pkg.Version);
            Assert.True(pkg.Enable);
        }
        finally
        {
            if (created) pkg10.Delete();
        }
    }
}
