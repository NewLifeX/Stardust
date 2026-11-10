using System;
using System.Collections.Generic;
using System.IO;
using StarAgent;
using Xunit;

namespace Stardust.Tests;

/// <summary>升级预检测试</summary>
public class UpgradePreflightTests
{
    private const String _app = "StarAgent";

    /// <summary>创建带 runtimeconfig.json 的临时应用目录</summary>
    private static String CreateApp(String? runtimeConfig)
    {
        var dir = Path.Combine(Path.GetTempPath(), "staragent-preflight-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        if (runtimeConfig != null) File.WriteAllText(Path.Combine(dir, _app + ".runtimeconfig.json"), runtimeConfig);

        return dir;
    }

    private static IList<String> Installed(params String[] versions) => new List<String>(versions);

    [Fact(DisplayName = "没有 runtimeconfig 时无法判定，不拦截升级")]
    public void NoRuntimeConfig_Unknown()
    {
        var dir = CreateApp(null);
        try
        {
            Assert.Null(UpgradePreflight.CheckRuntime(dir, _app, e => Installed("8.0.2")));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact(DisplayName = "新版本要求更高主版本运行时时判定为跑不起来")]
    public void HigherMajorMissing()
    {
        var dir = CreateApp("""{ "runtimeOptions": { "tfm": "net9.0", "framework": { "name": "Microsoft.NETCore.App", "version": "9.0.0" } } }""");
        try
        {
            var msg = UpgradePreflight.CheckRuntime(dir, _app, e => Installed("8.0.2"));

            Assert.NotNull(msg);
            Assert.Contains("9.0.0", msg);
            Assert.Contains("8.0.2", msg);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact(DisplayName = "同主版本更高次版本满足要求")]
    public void SameMajorMinorSatisfied()
    {
        var dir = CreateApp("""{ "runtimeOptions": { "framework": { "name": "Microsoft.NETCore.App", "version": "8.0.0" } } }""");
        try
        {
            Assert.Null(UpgradePreflight.CheckRuntime(dir, _app, e => Installed("8.0.2")));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact(DisplayName = "默认前滚策略不允许跨主版本")]
    public void HigherMajorInstalledIsNotEnough()
    {
        var dir = CreateApp("""{ "runtimeOptions": { "framework": { "name": "Microsoft.NETCore.App", "version": "8.0.0" } } }""");
        try
        {
            Assert.NotNull(UpgradePreflight.CheckRuntime(dir, _app, e => Installed("9.0.1")));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact(DisplayName = "显式 Major 前滚策略允许跨主版本")]
    public void RollForwardMajorSatisfied()
    {
        var dir = CreateApp("""{ "runtimeOptions": { "rollForward": "Major", "framework": { "name": "Microsoft.NETCore.App", "version": "8.0.0" } } }""");
        try
        {
            Assert.Null(UpgradePreflight.CheckRuntime(dir, _app, e => Installed("9.0.1")));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact(DisplayName = "自包含发布不检查本机运行时")]
    public void SelfContainedIgnored()
    {
        var dir = CreateApp("""{ "runtimeOptions": { "includedFrameworks": [ { "name": "Microsoft.NETCore.App", "version": "9.0.0" } ] } }""");
        try
        {
            Assert.Null(UpgradePreflight.CheckRuntime(dir, _app, e => Installed("8.0.2")));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact(DisplayName = "探测不到已安装版本时不拦截升级")]
    public void UnknownInstalledNotBlocked()
    {
        var dir = CreateApp("""{ "runtimeOptions": { "framework": { "name": "Microsoft.NETCore.App", "version": "9.0.0" } } }""");
        try
        {
            Assert.Null(UpgradePreflight.CheckRuntime(dir, _app, e => Installed()));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact(DisplayName = "多框架发布逐个校验，缺失者写入说明")]
    public void MultipleFrameworksChecked()
    {
        var dir = CreateApp("""{ "runtimeOptions": { "frameworks": [ { "name": "Microsoft.NETCore.App", "version": "8.0.0" }, { "name": "Microsoft.AspNetCore.App", "version": "9.0.0" } ] } }""");
        try
        {
            var msg = UpgradePreflight.CheckRuntime(dir, _app, e => e == "Microsoft.NETCore.App" ? Installed("8.0.2") : Installed("8.0.2"));

            Assert.NotNull(msg);
            Assert.Contains("Microsoft.AspNetCore.App 9.0.0", msg);
            Assert.DoesNotContain("Microsoft.NETCore.App 8.0.0", msg);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact(DisplayName = "预览版运行时按主次版本比较")]
    public void PreviewVersionCompared()
    {
        var dir = CreateApp("""{ "runtimeOptions": { "framework": { "name": "Microsoft.NETCore.App", "version": "9.0.0-preview.7.24405.7" } } }""");
        try
        {
            Assert.Null(UpgradePreflight.CheckRuntime(dir, _app, e => Installed("9.0.0")));
            Assert.NotNull(UpgradePreflight.CheckRuntime(dir, _app, e => Installed("8.0.2")));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact(DisplayName = "同主版本补丁号不得低于要求")]
    public void PatchLevelCompared()
    {
        var dir = CreateApp("""{ "runtimeOptions": { "framework": { "name": "Microsoft.NETCore.App", "version": "8.0.5" } } }""");
        try
        {
            Assert.Null(UpgradePreflight.CheckRuntime(dir, _app, e => Installed("8.0.33")));
            Assert.NotNull(UpgradePreflight.CheckRuntime(dir, _app, e => Installed("8.0.3")));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact(DisplayName = "Disable 策略要求精确版本")]
    public void DisableRequiresExactVersion()
    {
        var dir = CreateApp("""{ "runtimeOptions": { "rollForward": "Disable", "framework": { "name": "Microsoft.NETCore.App", "version": "7.0.0" } } }""");
        try
        {
            Assert.Null(UpgradePreflight.CheckRuntime(dir, _app, e => Installed("7.0.0")));
            Assert.NotNull(UpgradePreflight.CheckRuntime(dir, _app, e => Installed("7.0.20")));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact(DisplayName = "LatestMinor 仍不允许跨主版本")]
    public void LatestMinorStaysInMajor()
    {
        var dir = CreateApp("""{ "runtimeOptions": { "rollForward": "LatestMinor", "framework": { "name": "Microsoft.NETCore.App", "version": "8.0.0" } } }""");
        try
        {
            Assert.Null(UpgradePreflight.CheckRuntime(dir, _app, e => Installed("8.0.1")));
            Assert.NotNull(UpgradePreflight.CheckRuntime(dir, _app, e => Installed("9.0.1")));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact(DisplayName = "环境变量前滚策略优先于 runtimeconfig")]
    public void EnvironmentRollForwardOverrides()
    {
        var dir = CreateApp("""{ "runtimeOptions": { "rollForward": "Disable", "framework": { "name": "Microsoft.NETCore.App", "version": "8.0.0" } } }""");
        try
        {
            Assert.NotNull(UpgradePreflight.CheckRuntime(dir, _app, e => Installed("9.0.1")));

            Environment.SetEnvironmentVariable("DOTNET_ROLL_FORWARD", "Major");
            Assert.Null(UpgradePreflight.CheckRuntime(dir, _app, e => Installed("9.0.1")));
        }
        finally
        {
            Environment.SetEnvironmentVariable("DOTNET_ROLL_FORWARD", null);
            Directory.Delete(dir, true);
        }
    }

    [Fact(DisplayName = "预览版运行时默认不作为候选，放开后也不顶替同版本正式版")]
    public void PrereleaseRuntimeRejectedByDefault()
    {
        var dir = CreateApp("""{ "runtimeOptions": { "framework": { "name": "Microsoft.NETCore.App", "version": "9.0.0" } } }""");
        try
        {
            // 同版本预览版顶替不了正式版，开不开开关都不行
            Assert.NotNull(UpgradePreflight.CheckRuntime(dir, _app, e => Installed("9.0.0-preview.7.24405.7")));

            // 更高补丁的预览版：默认不算候选，开启 DOTNET_ROLL_FORWARD_TO_PRERELEASE 后才可用
            Assert.NotNull(UpgradePreflight.CheckRuntime(dir, _app, e => Installed("9.0.1-preview.1")));

            Environment.SetEnvironmentVariable("DOTNET_ROLL_FORWARD_TO_PRERELEASE", "1");
            Assert.Null(UpgradePreflight.CheckRuntime(dir, _app, e => Installed("9.0.1-preview.1")));
        }
        finally
        {
            Environment.SetEnvironmentVariable("DOTNET_ROLL_FORWARD_TO_PRERELEASE", null);
            Directory.Delete(dir, true);
        }
    }
}