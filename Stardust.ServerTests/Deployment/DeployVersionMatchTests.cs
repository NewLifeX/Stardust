using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using NewLife.Security;
using Stardust.Data.Deployment;
using Stardust.Data.Nodes;
using Stardust.Models;
using Stardust.Server.Services;
using Xunit;

namespace ServerTest.Deployment;

/// <summary>部署版本平台匹配测试。覆盖节点系统/架构归类与多版本筛选（PUB-10 / 版本匹配）</summary>
public class DeployVersionMatchTests
{
    /// <summary>创建只用于版本匹配的服务实例。GetDeployVersion 不依赖注入字段</summary>
    private static DeployService CreateService() => (DeployService)RuntimeHelpers.GetUninitializedObject(typeof(DeployService));

    private static Node CreateNode(OSKinds osKind, String arch, String frameworks = null) => new()
    {
        Code = "nd_" + Rand.NextString(8),
        Name = "node-" + Rand.NextString(4),
        Enable = true,
        OSKind = osKind,
        Architecture = arch,
        Frameworks = frameworks,
    };

    [Fact(DisplayName = "多版本部署：glibc 系 Linux 节点选中 Linux 版本而不是 musl 版本")]
    public void GetDeployVersion_LinuxNode_PicksLinuxPackage()
    {
        var app = new AppDeploy { Name = "dep_" + Rand.NextString(6), Enable = true, MultiVersion = true };
        app.Insert();

        // 先生成 musl 版本（Id 更小），再生成 Linux 版本（Id 更大，应优先且匹配）
        var musl = new AppDeployVersion { DeployId = app.Id, Version = "1.0.0", OS = OSKind.LinuxMusl, Arch = CpuArch.X64, Enable = true };
        musl.Insert();
        var linux = new AppDeployVersion { DeployId = app.Id, Version = "1.0.1", OS = OSKind.Linux, Arch = CpuArch.X64, Enable = true };
        linux.Insert();

        try
        {
            // Ubuntu 属于 glibc 系 Linux
            var node = CreateNode(OSKinds.Ubuntu, "x64");
            var ver = CreateService().GetDeployVersion(app, node);

            Assert.NotNull(ver);
            Assert.Equal(OSKind.Linux, ver.OS);
            Assert.Equal("1.0.1", ver.Version);
        }
        finally
        {
            musl.Delete();
            linux.Delete();
            app.Delete();
        }
    }

    [Fact(DisplayName = "多版本部署：Alpine 节点选中 musl 版本")]
    public void GetDeployVersion_AlpineNode_PicksMuslPackage()
    {
        var app = new AppDeploy { Name = "dep_" + Rand.NextString(6), Enable = true, MultiVersion = true };
        app.Insert();

        var linux = new AppDeployVersion { DeployId = app.Id, Version = "2.0.0", OS = OSKind.Linux, Arch = CpuArch.X64, Enable = true };
        linux.Insert();
        var musl = new AppDeployVersion { DeployId = app.Id, Version = "2.0.1", OS = OSKind.LinuxMusl, Arch = CpuArch.X64, Enable = true };
        musl.Insert();

        try
        {
            var node = CreateNode(OSKinds.Alpine, "x64");
            var ver = CreateService().GetDeployVersion(app, node);

            Assert.NotNull(ver);
            Assert.Equal(OSKind.LinuxMusl, ver.OS);
        }
        finally
        {
            musl.Delete();
            linux.Delete();
            app.Delete();
        }
    }

    [Fact(DisplayName = "架构不匹配的版本被跳过")]
    public void GetDeployVersion_ArchMismatch_Skip()
    {
        var app = new AppDeploy { Name = "dep_" + Rand.NextString(6), Enable = true, MultiVersion = true };
        app.Insert();

        var arm = new AppDeployVersion { DeployId = app.Id, Version = "3.0.0", OS = OSKind.Linux, Arch = CpuArch.Arm64, Enable = true };
        arm.Insert();

        try
        {
            var node = CreateNode(OSKinds.Ubuntu, "x64");
            Assert.Null(CreateService().GetDeployVersion(app, node));
        }
        finally
        {
            arm.Delete();
            app.Delete();
        }
    }

    [Fact(DisplayName = "节点框架不满足目标框架时跳过该版本")]
    public void GetDeployVersion_FrameworkMismatch_Skip()
    {
        var app = new AppDeploy { Name = "dep_" + Rand.NextString(6), Enable = true, MultiVersion = true };
        app.Insert();

        var ver = new AppDeployVersion { DeployId = app.Id, Version = "4.0.0", OS = OSKind.Any, Arch = CpuArch.Any, TargetFramework = "net8.0", Enable = true };
        ver.Insert();

        try
        {
            // 节点以裸版本号上报框架（如 6.0.21,7.0.10）
            var node = CreateNode(OSKinds.Ubuntu, "x64", "6.0.21");
            Assert.Null(CreateService().GetDeployVersion(app, node));

            // 框架满足时可以选中
            var node2 = CreateNode(OSKinds.Ubuntu, "x64", "8.0.11");
            Assert.NotNull(CreateService().GetDeployVersion(app, node2));
        }
        finally
        {
            ver.Delete();
            app.Delete();
        }
    }

    [Fact(DisplayName = "未开启多版本时按部署集指定版本精确匹配")]
    public void GetDeployVersion_SingleVersion_ByVersion()
    {
        var app = new AppDeploy { Name = "dep_" + Rand.NextString(6), Enable = true, MultiVersion = false, Version = "9.9.9" };
        app.Insert();

        var ver = new AppDeployVersion { DeployId = app.Id, Version = "9.9.9", OS = OSKind.Any, Arch = CpuArch.Any, Enable = true };
        ver.Insert();

        try
        {
            var node = CreateNode(OSKinds.Win10, "x64");
            var rs = CreateService().GetDeployVersion(app, node);

            Assert.NotNull(rs);
            Assert.Equal("9.9.9", rs.Version);
        }
        finally
        {
            ver.Delete();
            app.Delete();
        }
    }
}
