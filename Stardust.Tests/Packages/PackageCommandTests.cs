using System;
using System.IO;
using NewLife;
using StarAgent;
using Xunit;

namespace Stardust.Tests.Packages;

/// <summary>star 命令入口测试。覆盖命令识别、帮助与别名创建（PUB-25）</summary>
public class PackageCommandTests
{
    [Fact(DisplayName = "帮助命令被识别")]
    public void TryRun_Help_ReturnsTrue()
    {
        Assert.True(PackageCommand.TryRun(["--help"]));
        Assert.True(PackageCommand.TryRun(["-h"]));
        Assert.True(PackageCommand.TryRun(["help"]));
    }

    [Fact(DisplayName = "版本命令被识别")]
    public void TryRun_Version_ReturnsTrue()
    {
        Assert.True(PackageCommand.TryRun(["--version"]));
    }

    [Theory(DisplayName = "非 star 命令交回原有逻辑")]
    [InlineData("-install")]
    [InlineData("-run")]
    [InlineData("-s")]
    [InlineData("some.zip")]
    public void TryRun_ServiceOption_ReturnsFalse(String arg)
    {
        Assert.False(PackageCommand.TryRun([arg]));
    }

    [Fact(DisplayName = "空参数交回原有逻辑")]
    public void TryRun_Empty_ReturnsFalse()
    {
        Assert.False(PackageCommand.TryRun([]));
    }

    [Fact(DisplayName = "创建 star 命令别名")]
    public void EnsureAlias_CreatesWrapper()
    {
        Assert.True(StarLauncher.EnsureAlias());

        var dir = AppDomain.CurrentDomain.BaseDirectory;
        var file = Runtime.Windows ? Path.Combine(dir, "star.cmd") : Path.Combine(dir, "star");
        Assert.True(File.Exists(file));

        var content = File.ReadAllText(file);
        if (Runtime.Windows)
        {
            Assert.Contains("StarAgent", content);
        }
        else
        {
            Assert.Contains("#!/bin/sh", content);
            Assert.Contains("StarAgent", content);
        }
    }
}
