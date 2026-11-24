using System;
using System.Linq;
using NewLife.Caching;
using NewLife.Security;
using NewLife.Serialization;
using Stardust.Data.Deployment;
using Stardust.Data.Nodes;
using Stardust.Data.Platform;
using Stardust.Models;
using Stardust.Server.Services;
using Xunit;

namespace ServerTest.Deployment;

/// <summary>应用包服务测试。应用包即发布中心部署集，覆盖当前版本、平台匹配、短名解析与禁用过滤（PUB-25）</summary>
public class PackageServiceTests
{
    private static PackageService CreateService() => new(new DownloadTokenService(new CacheProvider()));

    private static AppDeploy CreatePackage(String? name = null, GalaxyProject? project = null) => new()
    {
        Name = name ?? "pkg_" + Rand.NextString(8),
        ProjectId = project?.Id ?? 0,
        Category = "test",
        Enable = true,
    };

    private static AppDeployVersion CreateVersion(AppDeploy package, String version, OSKind os, CpuArch arch, Boolean enable = true, DateTime? createTime = null, String targetFramework = "") => new()
    {
        DeployId = package.Id,
        Version = version,
        Enable = enable,
        OS = os,
        Arch = arch,
        TargetFramework = targetFramework,
        Url = $"/cube/file?id={Rand.Next(1000, 9999)}.zip",
        Hash = "hash",
        Size = 1024,
        CreateTime = createTime ?? DateTime.Now,
    };

    private static void SetCurrent(AppDeploy package, String version)
    {
        package.Version = version;
        package.Update();
    }

    private static void Cleanup(AppDeploy package, GalaxyProject? project = null)
    {
        foreach (var item in AppDeployVersion.FindAllByDeployId(package.Id, 1000)) item.Delete();
        package.Delete();
        project?.Delete();
    }

    [Fact(DisplayName = "按节点平台选择当前版本：Ubuntu 选 Linux 而不是更新的 musl 版本")]
    public void GetLatest_MatchesPlatform()
    {
        var package = CreatePackage();
        package.Insert();

        var linux = CreateVersion(package, "1.0", OSKind.Linux, CpuArch.X64, createTime: DateTime.Now.AddMinutes(-5));
        linux.Insert();
        var musl = CreateVersion(package, "2.0", OSKind.LinuxMusl, CpuArch.X64, createTime: DateTime.Now);
        musl.Insert();
        SetCurrent(package, "1.0");

        try
        {
            var node = new Node { OSKind = OSKinds.Ubuntu, Architecture = "x64" };

            var latest = CreateService().GetLatest(package.FullName, 0, 0, node);

            Assert.NotNull(latest);
            Assert.Equal("1.0", latest.Version);
            Assert.Equal(OSKind.Linux, latest.OS);
            Assert.Contains("token=", latest.Url);
        }
        finally
        {
            Cleanup(package);
        }
    }

    [Fact(DisplayName = "指定版本时按平台匹配，不拿其它系统的同版本包")]
    public void GetLatest_PinnedVersion_MatchesPlatform()
    {
        var package = CreatePackage();
        package.Insert();

        var win = CreateVersion(package, "1.0", OSKind.Windows, CpuArch.X64, createTime: DateTime.Now.AddMinutes(-5));
        win.Insert();
        var linux = CreateVersion(package, "1.0", OSKind.Linux, CpuArch.X64, createTime: DateTime.Now);
        linux.Insert();
        SetCurrent(package, "1.0");

        try
        {
            var latest = CreateService().GetLatest(package.FullName, (Int32)OSKind.Linux, (Int32)CpuArch.X64, null, "1.0");

            Assert.NotNull(latest);
            Assert.Equal(OSKind.Linux, latest.OS);
        }
        finally
        {
            Cleanup(package);
        }
    }

    [Fact(DisplayName = "同版本不同目标框架时按节点框架匹配")]
    public void GetLatest_MatchesTargetFramework()
    {
        var package = CreatePackage();
        package.Insert();

        CreateVersion(package, "1.0", OSKind.Any, CpuArch.Any, targetFramework: "net6.0").Insert();
        CreateVersion(package, "1.0", OSKind.Any, CpuArch.Any, targetFramework: "net8.0").Insert();
        SetCurrent(package, "1.0");

        try
        {
            var node = new Node { OSKind = OSKinds.Ubuntu, Architecture = "x64", Frameworks = "net8.0" };

            var latest = CreateService().GetLatest(package.FullName, 0, 0, node);

            Assert.NotNull(latest);
            Assert.Equal("net8.0", latest.TargetFramework);
        }
        finally
        {
            Cleanup(package);
        }
    }

    [Fact(DisplayName = "新上传但未应用的版本不会被拉取")]
    public void GetLatest_UsesAppliedVersion()
    {
        var package = CreatePackage();
        package.Insert();

        CreateVersion(package, "1.0", OSKind.Any, CpuArch.Any, createTime: DateTime.Now.AddMinutes(-5)).Insert();
        CreateVersion(package, "2.0", OSKind.Any, CpuArch.Any, createTime: DateTime.Now).Insert();
        SetCurrent(package, "1.0");

        try
        {
            var latest = CreateService().GetLatest(package.FullName, 0, 0, null);

            Assert.NotNull(latest);
            Assert.Equal("1.0", latest.Version);
        }
        finally
        {
            Cleanup(package);
        }
    }

    [Fact(DisplayName = "显式指定版本时可以拉取非当前版本")]
    public void GetLatest_ExplicitVersion_AllowsRollback()
    {
        var package = CreatePackage();
        package.Insert();

        CreateVersion(package, "1.0", OSKind.Any, CpuArch.Any, createTime: DateTime.Now.AddMinutes(-5)).Insert();
        CreateVersion(package, "2.0", OSKind.Any, CpuArch.Any, createTime: DateTime.Now).Insert();
        SetCurrent(package, "1.0");

        try
        {
            var latest = CreateService().GetLatest(package.FullName, 0, 0, null, "2.0");

            Assert.NotNull(latest);
            Assert.Equal("2.0", latest.Version);
        }
        finally
        {
            Cleanup(package);
        }
    }

    [Fact(DisplayName = "项目/应用全名与唯一短名都可以解析")]
    public void GetLatest_ProjectAndShortName_Resolves()
    {
        var project = new GalaxyProject { Name = "pub_" + Rand.NextString(6), Enable = true };
        project.Insert();

        var package = CreatePackage(project: project);
        package.Insert();
        CreateVersion(package, "1.0", OSKind.Any, CpuArch.Any).Insert();
        SetCurrent(package, "1.0");

        try
        {
            Assert.Equal($"{project.Name}/{package.Name}", package.FullName);

            var latest = CreateService().GetLatest(package.FullName, (Int32)OSKind.Any, (Int32)CpuArch.Any, null);
            Assert.NotNull(latest);
            Assert.Equal(package.FullName, latest.Name);

            var shortName = CreateService().GetLatest(package.Name, (Int32)OSKind.Any, (Int32)CpuArch.Any, null);
            Assert.NotNull(shortName);
            Assert.Equal(package.FullName, shortName.Name);

            Assert.Null(CreateService().GetLatest($"other/{package.Name}", (Int32)OSKind.Any, (Int32)CpuArch.Any, null));
        }
        finally
        {
            Cleanup(package, project);
        }
    }

    [Fact(DisplayName = "当前版本被禁用时不参与选择")]
    public void GetLatest_DisabledVersion_Ignored()
    {
        var package = CreatePackage();
        package.Insert();
        CreateVersion(package, "1.0", OSKind.Any, CpuArch.Any, enable: false).Insert();
        SetCurrent(package, "1.0");

        try
        {
            Assert.Null(CreateService().GetLatest(package.FullName, 0, 0, null));
        }
        finally
        {
            Cleanup(package);
        }
    }

    [Fact(DisplayName = "禁用包不可查询")]
    public void Get_DisabledPackage_ReturnsNull()
    {
        var package = CreatePackage();
        package.Enable = false;
        package.Insert();

        try
        {
            Assert.Null(CreateService().Get(package.FullName));
        }
        finally
        {
            Cleanup(package);
        }
    }

    [Fact(DisplayName = "搜索只返回已设置当前版本且启用的应用包")]
    public void Search_ReturnsEnabledPackages()
    {
        var name = "pkg_" + Rand.NextString(8);
        var package = CreatePackage(name);
        package.Insert();
        CreateVersion(package, "1.0", OSKind.Any, CpuArch.Any).Insert();
        SetCurrent(package, "1.0");

        var disabled = CreatePackage(name + "x");
        disabled.Enable = false;
        disabled.Insert();
        CreateVersion(disabled, "1.0", OSKind.Any, CpuArch.Any).Insert();
        SetCurrent(disabled, "1.0");

        var empty = CreatePackage(name + "e");
        empty.Insert();

        try
        {
            var list = CreateService().Search(name, 1, 20);

            Assert.Contains(list, e => e.Name == package.FullName);
            Assert.DoesNotContain(list, e => e.Name == disabled.FullName);
            Assert.DoesNotContain(list, e => e.Name == empty.FullName);
        }
        finally
        {
            Cleanup(package);
            Cleanup(disabled);
            Cleanup(empty);
        }
    }

    [Fact(DisplayName = "按编号下载时返回合成清单")]
    public void Download_ReturnsManifest()
    {
        var package = CreatePackage();
        package.FileName = "app.dll";
        package.Insert();
        var version = CreateVersion(package, "1.0", OSKind.Any, CpuArch.Any);
        version.Insert();
        SetCurrent(package, "1.0");

        try
        {
            var info = CreateService().Download(version.Id);

            Assert.NotNull(info);
            Assert.Equal(package.FullName, info.Name);
            Assert.Equal("app.dll", info.Manifest.ToJsonEntity<StarPackage>().Apps[0].FileName);
        }
        finally
        {
            Cleanup(package);
        }
    }
}