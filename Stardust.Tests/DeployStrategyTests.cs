using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using NewLife;
using StarAgent.Managers;
using Stardust.Models;
using Xunit;

namespace Stardust.Tests;

/// <summary>部署策略测试。覆盖工厂映射、模式能力开关与解压行为（PUB-18）</summary>
public class DeployStrategyTests
{
    /// <summary>创建临时工作目录</summary>
    private static String CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "stardust-deploy-" + Guid.NewGuid().ToString("n")[..8]);
        Directory.CreateDirectory(dir);
        return dir;
    }

    /// <summary>创建包含指定文件的压缩包</summary>
    private static String CreateZip(String dir, params String[] files)
    {
        var zip = Path.Combine(dir, "app-" + Guid.NewGuid().ToString("n")[..8] + ".zip");
        using var fs = File.Create(zip);
        using var za = new ZipArchive(fs, ZipArchiveMode.Create);
        foreach (var item in files)
        {
            var entry = za.CreateEntry(item);
            using var es = entry.Open();
            using var sw = new StreamWriter(es);
            sw.Write("content of " + item);
        }

        return zip;
    }

    private static DeployContext CreateContext(String name, String workDir, String? zipFile, ServiceInfo? service = null) => new()
    {
        Name = name,
        Service = service ?? new ServiceInfo { Name = name, FileName = name + ".dll", Enable = true },
        WorkingDirectory = workDir,
        ZipFile = zipFile,
    };

    [Fact(DisplayName = "工厂按新版模式创建对应策略")]
    public void Factory_NewModes()
    {
        Assert.IsType<StandardDeployStrategy>(DeployStrategyFactory.Create(DeployMode.Standard));
        Assert.IsType<ShadowDeployStrategy>(DeployStrategyFactory.Create(DeployMode.Shadow));
        Assert.IsType<HostedStrategy>(DeployStrategyFactory.Create(DeployMode.Hosted));
        Assert.IsType<TaskStrategy>(DeployStrategyFactory.Create(DeployMode.Task));
    }

    [Fact(DisplayName = "旧版模式值按兼容规则映射到新策略")]
    public void Factory_LegacyModes()
    {
        Assert.IsType<ShadowDeployStrategy>(DeployStrategyFactory.Create((DeployMode)0));
        Assert.IsType<HostedStrategy>(DeployStrategyFactory.Create((DeployMode)1));
        Assert.IsType<StandardDeployStrategy>(DeployStrategyFactory.Create((DeployMode)2));
        Assert.IsType<TaskStrategy>(DeployStrategyFactory.Create((DeployMode)3));
        Assert.IsType<ShadowDeployStrategy>(DeployStrategyFactory.Create((DeployMode)4));

        // 未知新版值兜底标准模式
        Assert.IsType<StandardDeployStrategy>(DeployStrategyFactory.Create((DeployMode)99));
    }

    [Fact(DisplayName = "服务信息为空时按标准模式处理")]
    public void Factory_NullService_Standard()
    {
        Assert.IsType<StandardDeployStrategy>(DeployStrategyFactory.Create((ServiceInfo)null));

        var service = new ServiceInfo { Name = "app", FileName = "app.dll", Mode = DeployMode.Task };
        Assert.IsType<TaskStrategy>(DeployStrategyFactory.Create(service));
    }

    [Fact(DisplayName = "各模式守护与接管能力符合约定")]
    public void Strategy_Capabilities()
    {
        var standard = new StandardDeployStrategy();
        Assert.True(standard.NeedGuardian);
        Assert.True(standard.AllowTakeOver);

        var shadow = new ShadowDeployStrategy();
        Assert.True(shadow.NeedGuardian);

        var hosted = new HostedStrategy();
        Assert.False(hosted.NeedGuardian);

        var task = new TaskStrategy();
        Assert.False(task.NeedGuardian);
        Assert.False(task.AllowTakeOver);
    }

    [Fact(DisplayName = "多实例判定兼容新属性与旧模式值")]
    public void Factory_IsMultipleAllowed()
    {
        Assert.False(DeployStrategyFactory.IsMultipleAllowed(null));
        Assert.True(DeployStrategyFactory.IsMultipleAllowed(new ServiceInfo { AllowMultiple = true }));
        Assert.True(DeployStrategyFactory.IsMultipleAllowed(new ServiceInfo { Mode = (DeployMode)4 }));
        Assert.False(DeployStrategyFactory.IsMultipleAllowed(new ServiceInfo { Mode = DeployMode.Standard }));
    }

    [Fact(DisplayName = "标准模式解压压缩包到工作目录并定位可执行文件")]
    public void Standard_Extract_ToWorkingDirectory()
    {
        var dir = CreateTempDir();
        try
        {
            var name = "demoapp";
            var zip = CreateZip(dir, name + ".dll", "appsettings.json");
            var workDir = Path.Combine(dir, "work");

            var ctx = CreateContext(name, workDir, zip);
            var rs = new StandardDeployStrategy().Extract(ctx);

            Assert.True(rs);
            Assert.True(File.Exists(Path.Combine(workDir, name + ".dll")));
            Assert.True(File.Exists(Path.Combine(workDir, "appsettings.json")));
            Assert.Equal(name + ".dll", Path.GetFileName(ctx.ExecuteFile));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact(DisplayName = "影子模式解压到独立影子目录，配置文件复制到工作目录")]
    public void Shadow_Extract_ToShadowDirectory()
    {
        var dir = CreateTempDir();
        try
        {
            var name = "shadowapp";
            var zip = CreateZip(dir, name + ".dll", "appsettings.json");
            var workDir = Path.Combine(dir, "work");
            Directory.CreateDirectory(workDir);

            var ctx = CreateContext(name, workDir, zip);
            var rs = new ShadowDeployStrategy().Extract(ctx);

            Assert.True(rs);
            Assert.False(ctx.Shadow.IsNullOrEmpty());

            // 可执行文件在影子目录，配置文件被复制到工作目录
            Assert.True(File.Exists(Path.Combine(ctx.Shadow, name + ".dll")));
            Assert.Equal(name + ".dll", Path.GetFileName(ctx.ExecuteFile));
            Assert.True(File.Exists(Path.Combine(workDir, "appsettings.json")));
            Assert.False(File.Exists(Path.Combine(workDir, name + ".dll")));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact(DisplayName = "缺少压缩包时标准模式直接使用工作目录")]
    public void Standard_NoZip_UseWorkingDirectory()
    {
        var dir = CreateTempDir();
        try
        {
            var name = "nozipapp";
            File.WriteAllText(Path.Combine(dir, name + ".dll"), "binary");

            var ctx = CreateContext(name, dir, null);
            var rs = new StandardDeployStrategy().Extract(ctx);

            Assert.True(rs);
            Assert.Equal(name + ".dll", Path.GetFileName(ctx.ExecuteFile));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact(DisplayName = "压缩包不存在且工作目录无可执行文件时解压失败")]
    public void Extract_MissingZip_Fail()
    {
        var dir = CreateTempDir();
        try
        {
            // 带路径分隔符的 FileName 不会当作系统命令，文件不存在则失败
            var service = new ServiceInfo { Name = "missingapp", FileName = "bin/missingapp.dll", Enable = true };
            var ctx = CreateContext("missingapp", Path.Combine(dir, "work"), Path.Combine(dir, "not-exist.zip"), service);
            var rs = new StandardDeployStrategy().Extract(ctx);

            Assert.False(rs);
            Assert.True(ctx.ExecuteFile.IsNullOrEmpty());
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }
}
