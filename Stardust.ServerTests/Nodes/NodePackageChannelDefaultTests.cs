using NewLife;
using Stardust.Data.Nodes;
using Stardust.Models;
using Xunit;

namespace ServerTest.Nodes;

/// <summary>安装包与发布版本默认通道测试。
/// 语义：Channel 未设置（0）时插入自动补 Release，否则会被 GetValids 的通道过滤掉，
/// 表现为后台记录 Enabled=true 但节点永远收不到升级/运行时包。</summary>
public class NodePackageChannelDefaultTests
{
    /// <summary>新建运行时包时通道为 0，插入后应补成 Release，并能被 Match 匹配到。</summary>
    [Fact]
    [Trait("Category", "Nodes")]
    public void DotNetPackage_DefaultsToReleaseChannel()
    {
        var node = new Node
        {
            Code = "test-ch-" + Guid.NewGuid().ToString("n")[..8],
            Name = "test-ch",
            ProductCode = "StarAgent-Cpp",
            Runtime = "cpp20",
            OSKind = OSKinds.Ubuntu,
            Architecture = "Arm64",
            Version = "3.6.2026.0104",
            Enable = true,
        };

        var pkg = new DotNetPackage
        {
            Version = "9.9." + Guid.NewGuid().ToString("n")[..8],
            Kind = "runtime",
            OSKind = OSKind.Linux,
            Architecture = CpuArch.Arm64,
            FileName = "dotnet-runtime-test.tar.gz",
            Source = "https://example.com/dotnet/test.tar.gz",
            Size = 1024,
            FileHash = "0123456789abcdef0123456789abcdef",
            Enable = true,
            // 故意不设置 Channel，模拟后台新建时采用默认值 0
        };

        try
        {
            pkg.Insert();

            Assert.Equal(NodeChannels.Release, pkg.Channel);

            // 通道补齐后，GetValids 才能把它取出来参与匹配
            var matched = DotNetPackage.Match(node, "runtime");
            Assert.NotNull(matched);
            Assert.Equal(pkg.Id, matched!.Id);
        }
        finally
        {
            pkg.Delete();
        }
    }

    /// <summary>显式指定其它通道时不被改写。</summary>
    [Fact]
    [Trait("Category", "Nodes")]
    public void ExplicitChannelIsKept()
    {
        var pkg = new DotNetPackage
        {
            Version = "9.9." + Guid.NewGuid().ToString("n")[..8],
            Kind = "runtime",
            OSKind = OSKind.Any,
            Architecture = CpuArch.Any,
            FileName = "dotnet-runtime-beta.tar.gz",
            Source = "https://example.com/dotnet/beta.tar.gz",
            Size = 1024,
            FileHash = "0123456789abcdef0123456789abcdef",
            Enable = false,
            Channel = NodeChannels.Beta,
        };

        try
        {
            pkg.Insert();
            Assert.Equal(NodeChannels.Beta, pkg.Channel);
        }
        finally
        {
            pkg.Delete();
        }
    }

    /// <summary>发布版本同样在通道为 0 时补 Release，否则版本永远不会被下发。</summary>
    [Fact]
    [Trait("Category", "Nodes")]
    public void ProductRelease_DefaultsToReleaseChannel()
    {
        var release = new ProductRelease
        {
            Version = "9.9.9999." + Guid.NewGuid().ToString("n")[..8],
            ProductCode = "StarAgent",
            Enable = false,
            Force = false,
        };

        try
        {
            release.Insert();
            Assert.Equal(NodeChannels.Release, release.Channel);
        }
        finally
        {
            release.Delete();
        }
    }
}
