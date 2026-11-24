using System;
using System.IO;
using System.IO.Compression;
using NewLife;
using Stardust.Deployment;
using Stardust.Models;
using Xunit;

namespace Stardust.Tests;

/// <summary>部署备份存储测试。覆盖提升、恢复、哈希校验与孤儿清理</summary>
public class DeployBackupStoreTests
{
    private static String CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "stardust-backup-" + Guid.NewGuid().ToString("n")[..8]);
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static String CreateZip(String path, String entry, String content)
    {
        if (File.Exists(path)) File.Delete(path);

        using var fs = File.Create(path);
        using var za = new ZipArchive(fs, ZipArchiveMode.Create);
        var item = za.CreateEntry(entry);
        using var es = item.Open();
        using var sw = new StreamWriter(es);
        sw.Write(content);

        return path;
    }

    private static String GetHash(String path) => new FileInfo(path).MD5().ToHex();

    [Fact(DisplayName = "稳定包提升为备份后可恢复，文件名保持下载时的名字")]
    public void Promote_And_Restore()
    {
        var root = CreateTempDir();
        var work = CreateTempDir();
        try
        {
            var store = new DeployBackupStore(root);
            var zip = CreateZip(Path.Combine(work, "stardust-1.0.zip"), "app.dll", "old");
            var hash = GetHash(zip);
            var deploy = new DeployInfo
            {
                Name = "app1",
                Version = "1.0.0",
                Hash = hash,
                Service = new ServiceInfo { Name = "app1", FileName = "app.dll" },
            };

            Assert.True(store.Promote("app1", zip, deploy));

            var backup = store.GetBackupZip("app1");
            Assert.NotNull(backup);
            Assert.Equal("stardust-1.0.zip", Path.GetFileName(backup));
            Assert.True(File.Exists(store.GetDeployFile("app1")));

            var saved = store.LoadDeploy("app1");
            Assert.NotNull(saved);
            Assert.Equal(hash, saved!.Hash);
            Assert.Equal("1.0.0", saved.Version);
            Assert.Equal("app.dll", saved.Service?.FileName);

            // 模拟新包覆盖当前 zip
            CreateZip(zip, "app.dll", "new");
            Assert.NotEqual(hash, GetHash(zip));

            var result = store.Restore("app1", zip, out var restored);
            Assert.Equal(DeployRestoreResult.Restored, result);
            Assert.NotNull(restored);
            Assert.Equal(hash, GetHash(zip));

            // 备份保留，再次回滚会发现当前包已是上次可用包
            Assert.True(File.Exists(backup));
            Assert.Equal(DeployRestoreResult.AlreadyGood, store.Restore("app1", zip, out _));
        }
        finally
        {
            try { Directory.Delete(root, true); } catch { }
            try { Directory.Delete(work, true); } catch { }
        }
    }

    [Fact(DisplayName = "提升时替换旧备份，目录只保留最新一个包")]
    public void Promote_ReplacesPreviousBackup()
    {
        var root = CreateTempDir();
        var work = CreateTempDir();
        try
        {
            var store = new DeployBackupStore(root);
            var v1 = CreateZip(Path.Combine(work, "v1.zip"), "app.dll", "v1");
            var v2 = CreateZip(Path.Combine(work, "v2.zip"), "app.dll", "v2");

            Assert.True(store.Promote("app1", v1, new DeployInfo { Name = "app1", Version = "1.0", Hash = GetHash(v1) }));
            Assert.True(store.Promote("app1", v2, new DeployInfo { Name = "app1", Version = "2.0", Hash = GetHash(v2) }));

            var files = Directory.GetFiles(store.GetBackupDir("app1"), "*.zip");
            Assert.Single(files);
            Assert.Equal("v2.zip", Path.GetFileName(files[0]));
            Assert.Equal("2.0", store.LoadDeploy("app1")!.Version);
        }
        finally
        {
            try { Directory.Delete(root, true); } catch { }
            try { Directory.Delete(work, true); } catch { }
        }
    }

    [Fact(DisplayName = "没有 DeployInfo 或哈希不匹配时不提升备份")]
    public void Promote_RequiresMatchingDeploy()
    {
        var root = CreateTempDir();
        var work = CreateTempDir();
        try
        {
            var store = new DeployBackupStore(root);
            var zip = CreateZip(Path.Combine(work, "app.zip"), "app.dll", "content");

            Assert.False(store.Promote("app1", zip, null));
            Assert.False(store.Promote("app1", zip, new DeployInfo { Name = "app1", Version = "1.0", Hash = "not-the-hash" }));
            Assert.Null(store.GetBackupZip("app1"));
        }
        finally
        {
            try { Directory.Delete(root, true); } catch { }
            try { Directory.Delete(work, true); } catch { }
        }
    }

    [Fact(DisplayName = "没有备份包时恢复返回 None")]
    public void Restore_NoBackup_ReturnsNone()
    {
        var root = CreateTempDir();
        var work = CreateTempDir();
        try
        {
            var store = new DeployBackupStore(root);
            var zip = CreateZip(Path.Combine(work, "app.zip"), "app.dll", "new");

            var result = store.Restore("app1", zip, out var backup);
            Assert.Equal(DeployRestoreResult.None, result);
            Assert.Null(backup);
        }
        finally
        {
            try { Directory.Delete(root, true); } catch { }
            try { Directory.Delete(work, true); } catch { }
        }
    }

    [Fact(DisplayName = "当前包已是上次可用包时恢复返回 AlreadyGood")]
    public void Restore_AlreadyGood()
    {
        var root = CreateTempDir();
        var work = CreateTempDir();
        try
        {
            var store = new DeployBackupStore(root);
            var zip = CreateZip(Path.Combine(work, "app.zip"), "app.dll", "good");
            var hash = GetHash(zip);
            Assert.True(store.Promote("app1", zip, new DeployInfo { Name = "app1", Version = "1.0", Hash = hash }));

            var result = store.Restore("app1", zip, out var backup);
            Assert.Equal(DeployRestoreResult.AlreadyGood, result);
            Assert.NotNull(backup);
            Assert.Equal(hash, backup!.Hash);
        }
        finally
        {
            try { Directory.Delete(root, true); } catch { }
            try { Directory.Delete(work, true); } catch { }
        }
    }

    [Fact(DisplayName = "备份包哈希与 deploy.json 不一致时拒绝恢复")]
    public void Restore_BackupHashMismatch_None()
    {
        var root = CreateTempDir();
        var work = CreateTempDir();
        try
        {
            var store = new DeployBackupStore(root);
            var goodZip = CreateZip(Path.Combine(work, "good.zip"), "app.dll", "good");
            Assert.True(store.Promote("app1", goodZip, new DeployInfo { Name = "app1", Version = "1.0", Hash = GetHash(goodZip) }));

            // 模拟备份包被破坏或写成半成品
            CreateZip(store.GetBackupZip("app1"), "other.txt", "other");

            var currentZip = CreateZip(Path.Combine(work, "app.zip"), "app.dll", "new");
            var currentHash = GetHash(currentZip);

            var result = store.Restore("app1", currentZip, out _);
            Assert.Equal(DeployRestoreResult.None, result);
            Assert.Equal(currentHash, GetHash(currentZip));
        }
        finally
        {
            try { Directory.Delete(root, true); } catch { }
            try { Directory.Delete(work, true); } catch { }
        }
    }

    [Fact(DisplayName = "清理只删除不在活动列表且超过保留期的目录")]
    public void Cleanup_RemovesOnlyOrphans()
    {
        var root = CreateTempDir();
        try
        {
            var active = Path.Combine(root, "active");
            var orphan = Path.Combine(root, "orphan");
            Directory.CreateDirectory(active);
            Directory.CreateDirectory(orphan);
            Directory.SetLastWriteTime(orphan, DateTime.Now.AddDays(-31));

            var store = new DeployBackupStore(root);
            var count = store.Cleanup(["active"], TimeSpan.FromDays(30));

            Assert.Equal(1, count);
            Assert.True(Directory.Exists(active));
            Assert.False(Directory.Exists(orphan));
        }
        finally
        {
            try { Directory.Delete(root, true); } catch { }
        }
    }
}