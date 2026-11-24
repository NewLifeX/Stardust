using System;
using System.IO;
using Moq;
using NewLife.Agent;
using NewLife.Log;
using StarAgent;
using Xunit;

namespace Stardust.Tests;

/// <summary>升级自愈测试</summary>
public class UpgradeHealerTests
{
    [Fact(DisplayName = "启动自愈清理升级备份与过期升级包")]
    public void Heal_CleanBackupsAndStalePackages()
    {
        var dir = Path.Combine(Path.GetTempPath(), "staragent-heal-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            // 旧版升级失败留下的备份（改名后未重启，老进程占用）
            var del = Path.Combine(dir, "StarAgent.exe.del");
            File.WriteAllText(del, "old");
            File.SetLastWriteTimeUtc(del, DateTime.UtcNow.AddHours(-1));
            var del2 = Path.Combine(dir, "Stardust.dll.del");
            File.WriteAllText(del2, "old");
            File.SetLastWriteTimeUtc(del2, DateTime.UtcNow.AddHours(-1));

            // 刚刚生成的备份要保留，供冒烟失败回滚
            var recent = Path.Combine(dir, "StarAgent.del");
            File.WriteAllText(recent, "recent");
            File.SetLastWriteTimeUtc(recent, DateTime.UtcNow);

            // 过期的升级包与新下载的升级包
            var update = Path.Combine(dir, "Update");
            Directory.CreateDirectory(update);
            var stale = Path.Combine(update, "staragent80.zip");
            File.WriteAllText(stale, "old");
            File.SetLastWriteTime(stale, DateTime.Now.AddDays(-5));
            var fresh = Path.Combine(update, "staragent10.zip");
            File.WriteAllText(fresh, "new");

            var msg = UpgradeHealer.Heal(null, Logger.Null, dir);

            Assert.False(File.Exists(del));
            Assert.False(File.Exists(del2));
            Assert.True(File.Exists(recent));
            Assert.False(File.Exists(stale));
            Assert.True(File.Exists(fresh));
            Assert.NotNull(msg);
            Assert.Contains("2", msg);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact(DisplayName = "启动入口修复把过期 apphost 换成 dotnet shim")]
    public void HealEntry_StaleApphost_ReplacedByShim()
    {
        var dir = Path.Combine(Path.GetTempPath(), "staragent-entry-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            // 升级包只换 dll，旧 apphost 停留在更早时间点
            var app = Path.Combine(dir, "StarAgent");
            File.WriteAllText(app, "old-apphost");
            File.SetLastWriteTimeUtc(app, DateTime.UtcNow.AddDays(-1));
            var dll = Path.Combine(dir, "StarAgent.dll");
            File.WriteAllText(dll, "new-dll");

            var msg = UpgradeHealer.HealEntry(dir, "StarAgent.dll", "/usr/share/dotnet/dotnet", Logger.Null);

            Assert.NotNull(msg);
            var shim = File.ReadAllText(app);
            Assert.StartsWith("#!/bin/sh", shim);
            Assert.Contains("/usr/share/dotnet/dotnet", shim);
            Assert.Contains("StarAgent.dll", shim);
            Assert.False(File.Exists(app + ".new"));

            // 启动脚本必须可执行，否则 init 脚本调用时报 Permission denied，节点再也起不来
            // net6 没有 File.SetUnixFileMode，老框架走 chmod 兜底，这里在 Linux 上统一校验结果
            if (OperatingSystem.IsLinux())
            {
                var mode = File.GetUnixFileMode(app);
                Assert.True(mode.HasFlag(UnixFileMode.UserExecute));
                Assert.True(mode.HasFlag(UnixFileMode.OtherExecute));
            }
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact(DisplayName = "apphost 与程序集同版本时不改动启动入口")]
    public void HealEntry_UpToDateApphost_Untouched()
    {
        var dir = Path.Combine(Path.GetTempPath(), "staragent-entry-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var app = Path.Combine(dir, "StarAgent");
            File.WriteAllText(app, "self-contained-apphost");
            var dll = Path.Combine(dir, "StarAgent.dll");
            File.WriteAllText(dll, "dll");
            File.SetLastWriteTimeUtc(dll, File.GetLastWriteTimeUtc(app).AddMinutes(-1));

            Assert.Null(UpgradeHealer.HealEntry(dir, "StarAgent.dll", "/usr/share/dotnet/dotnet", Logger.Null));
            Assert.Equal("self-contained-apphost", File.ReadAllText(app));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact(DisplayName = "按 runtimeconfig 判定框架依赖形态")]
    public void IsFrameworkDependent_DistinguishesAnyFromSelfContained()
    {
        var dir = Path.Combine(Path.GetTempPath(), "staragent-form-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            Assert.False(UpgradeHealer.IsFrameworkDependent(dir));

            var file = Path.Combine(dir, "StarAgent.runtimeconfig.json");
            File.WriteAllText(file, """{"runtimeOptions":{"framework":{"name":"Microsoft.NETCore.App","version":"10.0.0"}}}""");
            Assert.True(UpgradeHealer.IsFrameworkDependent(dir));

            File.WriteAllText(file, """{"runtimeOptions":{"includedFrameworks":[{"name":"Microsoft.NETCore.App","version":"10.0.0"}]}}""");
            Assert.False(UpgradeHealer.IsFrameworkDependent(dir));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact(DisplayName = "服务定义按 any 或原生形态选择启动入口")]
    public void BuildServiceModel_UsesExpectedEntry()
    {
        var service = new Mock<ServiceBase> { CallBase = true }.Object;
        service.ServiceName = "StarAgent";
        service.DisplayName = "StarAgent";
        service.Description = "StarAgent";

        var dir = Path.Combine(Path.GetTempPath(), "staragent-model-" + Guid.NewGuid().ToString("N"));
        try
        {
            var any = UpgradeHealer.BuildServiceModel(service, dir, true);
            Assert.StartsWith("dotnet", Path.GetFileName(any.FileName), StringComparison.OrdinalIgnoreCase);
            Assert.Contains(Path.Combine(dir, "StarAgent.dll"), any.Arguments);
            Assert.Contains("-s", any.Arguments);

            var native = UpgradeHealer.BuildServiceModel(service, dir, false);
            Assert.Equal(Path.Combine(dir, OperatingSystem.IsWindows() ? "StarAgent.exe" : "StarAgent"), native.FileName);
            Assert.Equal("-s", native.Arguments);
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }

    [Fact(DisplayName = "无残留时不产生自愈事件")]
    public void Heal_NothingToDo_ReturnNull()
    {
        var dir = Path.Combine(Path.GetTempPath(), "staragent-heal-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            Assert.Null(UpgradeHealer.Heal(null, Logger.Null, dir));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }
}
