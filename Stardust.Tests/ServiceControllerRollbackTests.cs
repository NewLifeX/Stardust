using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using NewLife;
using NewLife.Log;
using Stardust.Deployment;
using Stardust.Managers;
using Stardust.Models;
using Xunit;

namespace Stardust.Tests;

/// <summary>应用启动失败回滚测试。覆盖连续失败触发回滚、当前包已是可用包、回滚只执行一次</summary>
public class ServiceControllerRollbackTests
{
    private static String CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "stardust-rollback-" + Guid.NewGuid().ToString("n")[..8]);
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

    private static ServiceController CreateController(String name, String workDir, String zip, String? backupPath, List<String> logs)
    {
        var controller = new ServiceController
        {
            Name = name,
            MaxFails = 2,
            StartWait = 50,
            BackupPath = backupPath,
            StableMinutes = 0,
            Log = new ActionLog((format, args) =>
            {
                lock (logs)
                {
                    try { logs.Add(args == null || args.Length == 0 ? format : String.Format(format, args)); }
                    catch { logs.Add(format); }
                }
            }),
        };

        controller.SetInfo(new ServiceInfo
        {
            Name = name,
            // 带路径分隔符，找不到文件时直接失败，不会当作系统命令执行
            FileName = "bin/missing.dll",
            WorkingDirectory = workDir,
            Enable = true,
            Mode = DeployMode.Standard,
            ZipFile = zip,
        });

        return controller;
    }

    [Fact(DisplayName = "连续启动失败后回滚到上次可用包，且只回滚一次")]
    public void Start_MaxFails_RollsBackOnce()
    {
        var root = CreateTempDir();
        var work = CreateTempDir();
        var logs = new List<String>();
        ServiceController? controller = null;
        try
        {
            var name = "rollback-app";
            var store = new DeployBackupStore(root);

            var goodZip = CreateZip(Path.Combine(work, "good.zip"), "readme.txt", "good");
            var goodHash = GetHash(goodZip);
            var currentZip = CreateZip(Path.Combine(work, "app.zip"), "readme.txt", "bad");
            var badHash = GetHash(currentZip);

            Assert.True(store.Promote(name, goodZip, new DeployInfo { Name = name, Version = "1.0.0", Hash = goodHash }));

            controller = CreateController(name, work, currentZip, root, logs);
            controller.DeployInfo = new DeployInfo { Name = name, Version = "2.0.0", Hash = badHash };

            // 两次失败后第三次触发回滚，回滚后的旧包仍然找不到可执行文件，继续失败
            Assert.False(controller.Start());
            Assert.False(controller.Start());
            Assert.False(controller.Start());

            Assert.Equal(goodHash, GetHash(currentZip));
            Assert.Equal("1.0.0", controller.DeployInfo?.Version);

            // deploy.json 保留上次可用包的信息，不被失败版本覆盖
            Assert.Equal("1.0.0", store.LoadDeploy(name)?.Version);
            Assert.True(File.Exists(store.GetBackupZip(name)));

            // 服务端重复下发同一失败版本时，不能再清零失败计数
            var failedMethod = typeof(ServiceController).GetMethod("IsFailedVersion", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(failedMethod);
            Assert.True((Boolean)failedMethod!.Invoke(controller, [new DeployInfo { Hash = badHash }])!);
            Assert.False((Boolean)failedMethod.Invoke(controller, [new DeployInfo { Hash = goodHash }])!);

            // 旧包再次达到最大失败数时不允许二次回滚
            Assert.False(controller.Start());
            Assert.False(controller.Start());
            Assert.Equal(goodHash, GetHash(currentZip));
            Assert.True(File.Exists(store.GetBackupZip(name)));

            Assert.Equal(1, logs.Count(e => e.Contains("Rolled back") || e.Contains("回滚")));
        }
        finally
        {
            controller?.TryDispose();
            try { Directory.Delete(root, true); } catch { }
            try { Directory.Delete(work, true); } catch { }
        }
    }

    [Fact(DisplayName = "稳定运行满阈值后把当前包提升为回滚包")]
    public void Stable_Then_Promote()
    {
        var root = CreateTempDir();
        var work = CreateTempDir();
        var logs = new List<String>();
        ServiceController? controller = null;
        try
        {
            var name = "stable-app";
            var zip = CreateZip(Path.Combine(work, "app.zip"), "readme.txt", "stable");
            var hash = GetHash(zip);
            var store = new DeployBackupStore(root);

            controller = CreateController(name, work, zip, root, logs);
            controller.DeployInfo = new DeployInfo { Name = name, Version = "1.0.0", Hash = hash };
            controller.StableMinutes = 10;
            controller.StartTime = DateTime.Now.AddMinutes(-11);
            controller.Running = true;

            // 模拟 Start 记录过本次运行使用的包
            var zipField = typeof(ServiceController).GetField("_runZip", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(zipField);
            zipField!.SetValue(controller, zip);

            var hashField = typeof(ServiceController).GetField("_runHash", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(hashField);
            hashField!.SetValue(controller, hash);

            // 直接验证稳定阈值与提升逻辑，不启动真实进程
            var method = typeof(ServiceController).GetMethod("TryMarkGood", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(method);
            method!.Invoke(controller, null);

            Assert.True(File.Exists(store.GetBackupZip(name)));
            Assert.Equal(hash, store.LoadDeploy(name)?.Hash);
        }
        finally
        {
            controller?.TryDispose();
            try { Directory.Delete(root, true); } catch { }
            try { Directory.Delete(work, true); } catch { }
        }
    }

    [Fact(DisplayName = "当前包已经是上次可用包时不回滚")]
    public void Start_CurrentGood_DoesNotRollback()
    {
        var root = CreateTempDir();
        var work = CreateTempDir();
        var logs = new List<String>();
        ServiceController? controller = null;
        try
        {
            var name = "rollback-good";
            var store = new DeployBackupStore(root);
            var currentZip = CreateZip(Path.Combine(work, "app.zip"), "readme.txt", "good");
            var hash = GetHash(currentZip);

            Assert.True(store.Promote(name, currentZip, new DeployInfo { Name = name, Version = "1.0.0", Hash = hash }));

            controller = CreateController(name, work, currentZip, root, logs);
            controller.DeployInfo = new DeployInfo { Name = name, Version = "1.0.0", Hash = hash };

            Assert.False(controller.Start());
            Assert.False(controller.Start());
            Assert.False(controller.Start());

            Assert.Equal(hash, GetHash(currentZip));
            Assert.Equal("1.0.0", store.LoadDeploy(name)?.Version);
            Assert.True(File.Exists(store.GetBackupZip(name)));
            Assert.Equal("1.0.0", controller.DeployInfo?.Version);
        }
        finally
        {
            controller?.TryDispose();
            try { Directory.Delete(root, true); } catch { }
            try { Directory.Delete(work, true); } catch { }
        }
    }
}