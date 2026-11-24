using System;
using System.IO;
using System.Reflection;
using NewLife.Remoting.Clients;
using StarAgent;
using Xunit;

namespace Stardust.Tests;

/// <summary>跨形态升级备份测试</summary>
public class StarUpgradeTests
{
    private static readonly Type _type = typeof(StarService).Assembly.GetType("StarAgent.StarUpgrade", true)!;

    [Fact(DisplayName = "Linux 原生入口先备份为 .old，避免被基类清理")]
    public void BackupNativeEntry_UsesOldAndSurvivesDeleteBackup()
    {
        var dir = NewDir();
        var tmp = NewDir();
        try
        {
            var native = Path.Combine(dir, "StarAgent");
            File.WriteAllText(native, "native-aot");
            File.WriteAllText(Path.Combine(tmp, "StarAgent.dll"), "managed");

            var backup = Invoke("BackupNativeEntry", dir, tmp, true) as String;

            Assert.NotNull(backup);
            Assert.EndsWith(".old", backup);
            Assert.False(File.Exists(native));
            Assert.Equal("native-aot", File.ReadAllText(backup!));

            // 基类更新开头会删除所有 .del；.old 必须留下
            new Upgrade { DestinationPath = dir }.DeleteBackup(dir);
            Assert.True(File.Exists(backup));
        }
        finally
        {
            Cleanup(dir); Cleanup(tmp);
        }
    }

    [Fact(DisplayName = "非 Linux 不备份无扩展名入口")]
    public void BackupNativeEntry_NonLinux_ReturnsNull()
    {
        var dir = NewDir();
        var tmp = NewDir();
        try
        {
            var native = Path.Combine(dir, "StarAgent");
            File.WriteAllText(native, "native");
            File.WriteAllText(Path.Combine(tmp, "StarAgent"), "native");

            Assert.Null(Invoke("BackupNativeEntry", dir, tmp, false));
            Assert.True(File.Exists(native));
        }
        finally
        {
            Cleanup(dir); Cleanup(tmp);
        }
    }

    [Fact(DisplayName = "更新成功后备份提升为 .del 供回滚")]
    public void PromoteBackup_RenamesToDel()
    {
        var dir = NewDir();
        try
        {
            var native = Path.Combine(dir, "StarAgent");
            var backup = native + ".old";
            File.WriteAllText(backup, "old");

            var del = Invoke("PromoteBackup", backup, dir) as String;

            Assert.NotNull(del);
            Assert.EndsWith(".del", del);
            Assert.False(File.Exists(backup));
            Assert.Equal("old", File.ReadAllText(del!));
        }
        finally
        {
            Cleanup(dir);
        }
    }

    [Fact(DisplayName = "更新失败时恢复原生入口")]
    public void RestoreNativeEntry_ReplacesNewFile()
    {
        var dir = NewDir();
        try
        {
            var native = Path.Combine(dir, "StarAgent");
            var backup = native + ".old";
            File.WriteAllText(backup, "old");
            File.WriteAllText(native, "half-written-new");

            Assert.Equal(true, Invoke("RestoreNativeEntry", backup, dir));
            Assert.Equal("old", File.ReadAllText(native));
            Assert.False(File.Exists(backup));
        }
        finally
        {
            Cleanup(dir);
        }
    }

    [Fact(DisplayName = "any 升级到 AOT 后回滚恢复原生入口")]
    public void Update_AnyToAot_BacksUpAndRollsBack()
    {
        var dir = NewDir();
        var tmp = NewDir();
        try
        {
            File.WriteAllText(Path.Combine(dir, "StarAgent"), "old-native");
            File.WriteAllText(Path.Combine(dir, "StarAgent.dll"), "old-managed");
            File.WriteAllText(Path.Combine(dir, "StarAgent.runtimeconfig.json"), "{}");
            File.WriteAllText(Path.Combine(tmp, "StarAgent"), "new-native");

            var upgrade = NewUpgrade(dir, tmp);
            Assert.Equal(true, InvokeInstance(upgrade, "Update"));

            Assert.Equal("new-native", File.ReadAllText(Path.Combine(dir, "StarAgent")));
            var del = Path.Combine(dir, "StarAgent.del");
            Assert.True(File.Exists(del), String.Join(",", Directory.GetFiles(dir).Select(Path.GetFileName)));
            Assert.Equal("old-native", File.ReadAllText(del));

            Assert.True((Int32)InvokeInstance(upgrade, "Rollback")! > 0);
            Assert.Equal("old-native", File.ReadAllText(Path.Combine(dir, "StarAgent")));
        }
        finally
        {
            Cleanup(dir); Cleanup(tmp);
        }
    }

    [Fact(DisplayName = "AOT 升级到 any 后回滚恢复原生入口")]
    public void Update_AotToAny_BacksUpAndRollsBack()
    {
        var dir = NewDir();
        var tmp = NewDir();
        try
        {
            File.WriteAllText(Path.Combine(dir, "StarAgent"), "old-native");
            File.WriteAllText(Path.Combine(tmp, "StarAgent"), "#!/bin/sh\nexec dotnet StarAgent.dll\n");
            File.WriteAllText(Path.Combine(tmp, "StarAgent.dll"), "new-managed");
            File.WriteAllText(Path.Combine(tmp, "StarAgent.runtimeconfig.json"), "{}");

            var upgrade = NewUpgrade(dir, tmp);
            Assert.Equal(true, InvokeInstance(upgrade, "Update"));

            Assert.Equal("new-managed", File.ReadAllText(Path.Combine(dir, "StarAgent.dll")));
            var del = Path.Combine(dir, "StarAgent.del");
            Assert.True(File.Exists(del), String.Join(",", Directory.GetFiles(dir).Select(Path.GetFileName)));
            Assert.Equal("old-native", File.ReadAllText(del));

            Assert.True((Int32)InvokeInstance(upgrade, "Rollback")! > 0);
            Assert.Equal("old-native", File.ReadAllText(Path.Combine(dir, "StarAgent")));
        }
        finally
        {
            Cleanup(dir); Cleanup(tmp);
        }
    }

    private static Object NewUpgrade(String dest, String tmp)
    {
        var upgrade = Activator.CreateInstance(_type, true)!;
        var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        _type.GetProperty("DestinationPath", flags)!.SetValue(upgrade, dest);
        _type.GetProperty("TempPath", flags)!.SetValue(upgrade, tmp);
        _type.GetProperty("ForceLinux", flags)!.SetValue(upgrade, true);
        return upgrade;
    }

    private static Object? InvokeInstance(Object instance, String name, params Object?[] args)
    {
        var method = _type.GetMethod(name, BindingFlags.Public | BindingFlags.Instance);
        Assert.NotNull(method);

        return method!.Invoke(instance, args);
    }

    private static Object? Invoke(String name, params Object?[] args)
    {
        var method = _type.GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        return method!.Invoke(null, args);
    }

    private static void Cleanup(String dir)
    {
        if (Directory.Exists(dir)) Directory.Delete(dir, true);
    }

    private static String NewDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "staragent-upgrade-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }
}



