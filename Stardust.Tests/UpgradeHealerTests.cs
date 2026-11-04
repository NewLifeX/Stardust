using System;
using System.IO;
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
            var del2 = Path.Combine(dir, "Stardust.dll.del");
            File.WriteAllText(del2, "old");

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
