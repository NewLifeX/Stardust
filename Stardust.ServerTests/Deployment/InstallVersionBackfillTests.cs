extern alias web;

using System;
using System.Runtime.CompilerServices;
using NewLife.Security;
using Stardust.Data;
using Stardust.Data.Deployment;
using Stardust.Data.Nodes;
using Stardust.Models;
using Xunit;
using webDeploy = web::Stardust.Web.Services.DeployService;

namespace ServerTest.Deployment;

/// <summary>install 下发回填部署版本 + 应用程序版本回填测试（2026-11-12 方案C/新增 AppVersion 字段）</summary>
/// <remarks>
/// 方案C：点击「发布」（install）即回填 AppDeployNode.Version = 本次下发的版本（多版本取平台匹配包，单版本取部署集版本）；
/// AppVersion：应用在线实例上报的应用入口程序集 FileVersion，经 Fill(AppOnline)/Fill(AppInfo) 回填，非空守卫防清值。
/// 版本匹配共享实现已下沉 AppDeploy.GetDeployVersion（DeployVersionMatchTests 覆盖匹配细节，本文件只测回填接线）。
/// </remarks>
public class InstallVersionBackfillTests
{
    /// <summary>创建只用于 Install 回填的服务实例。Install 不依赖注入字段</summary>
    private static webDeploy CreateWebDeployService() => (webDeploy)RuntimeHelpers.GetUninitializedObject(typeof(webDeploy));

    private static Node CreateNode(OSKinds osKind, String arch, String frameworks = null) => new()
    {
        Code = "nd_" + Rand.NextString(8),
        Name = "node-" + Rand.NextString(4),
        Enable = true,
        OSKind = osKind,
        Architecture = arch,
        Frameworks = frameworks,
    };

    [Fact(DisplayName = "install 回填：多版本部署集按平台匹配版本回填节点版本")]
    public void Install_MultiVersion_BackfillsMatchedVersion()
    {
        var app = new AppDeploy { Name = "dep_" + Rand.NextString(6), Enable = true, MultiVersion = true };
        app.Insert();
        var node = CreateNode(OSKinds.Ubuntu, "x64");
        node.Insert();

        var win = new AppDeployVersion { DeployId = app.Id, Version = "1.0.0", OS = OSKind.Windows, Arch = CpuArch.X64, Enable = true };
        win.Insert();
        var linux = new AppDeployVersion { DeployId = app.Id, Version = "1.0.1", OS = OSKind.Linux, Arch = CpuArch.X64, Enable = true };
        linux.Insert();

        try
        {
            var dn = new AppDeployNode { DeployId = app.Id, NodeId = node.ID, Enable = true };
            dn.Insert();

            CreateWebDeployService().Install(dn);

            // Ubuntu 节点应拿到 Linux 包的版本串，而不是部署集/别的包
            Assert.True(dn.Enable);
            Assert.Equal("1.0.1", dn.Version);
        }
        finally
        {
            win.Delete();
            linux.Delete();
            node.Delete();
            app.Delete();
        }
    }

    [Fact(DisplayName = "install 回填：单版本部署集且版本表无匹配行时兜底部署集版本")]
    public void Install_NoMatchedVersion_FallsBackToAppVersion()
    {
        var app = new AppDeploy { Name = "dep_" + Rand.NextString(6), Enable = true, MultiVersion = true, Version = "2.0.0" };
        app.Insert();
        var node = CreateNode(OSKinds.Ubuntu, "x64");
        node.Insert();

        try
        {
            var dn = new AppDeployNode { DeployId = app.Id, NodeId = node.ID, Enable = true };
            dn.Insert();

            CreateWebDeployService().Install(dn);

            // 版本表无匹配行（与服务端 BuildDeployInfo 无条件 app.Version 同一口径的兜底）
            Assert.Equal("2.0.0", dn.Version);
        }
        finally
        {
            node.Delete();
            app.Delete();
        }
    }

    [Fact(DisplayName = "install 回填：部署集无版本时不写入（保持原值）")]
    public void Install_NoVersionAnywhere_KeepsOriginalValue()
    {
        var app = new AppDeploy { Name = "dep_" + Rand.NextString(6), Enable = true, MultiVersion = false };
        app.Insert();
        var node = CreateNode(OSKinds.Ubuntu, "x64");
        node.Insert();

        try
        {
            var dn = new AppDeployNode { DeployId = app.Id, NodeId = node.ID, Enable = true, Version = "keep" };
            dn.Insert();

            CreateWebDeployService().Install(dn);

            Assert.True(dn.Enable);
            Assert.Equal("keep", dn.Version);
        }
        finally
        {
            node.Delete();
            app.Delete();
        }
    }

    [Fact(DisplayName = "Fill(AppOnline)：在线实例版本回填 AppVersion，空值不清已有版本（守卫）")]
    public void FillOnline_AppVersion_Guarded()
    {
        var dn = new AppDeployNode { DeployId = 1, NodeId = 1, AppVersion = "9.9.9.9" };

        dn.Fill(new AppOnline { IP = "127.0.0.1", Version = "1.2.3.0" });
        Assert.Equal("1.2.3.0", dn.AppVersion);

        // 心跳/在线刷新未带版本（如 StarAgent 代上报）时不得清空已登记的应用程序版本
        dn.Fill(new AppOnline { IP = "127.0.0.1" });
        Assert.Equal("1.2.3.0", dn.AppVersion);
    }

    [Fact(DisplayName = "Fill(AppInfo)：进程上报的客户端文件版本回填 AppVersion，空值守卫")]
    public void FillAppInfo_AppVersion_Guarded()
    {
        var dn = new AppDeployNode { DeployId = 1, NodeId = 1 };

        dn.Fill(new AppInfo { Id = 123, Name = "test", Version = "2.0.0.0" });
        Assert.Equal("2.0.0.0", dn.AppVersion);

        dn.Fill(new AppInfo { Id = 123, Name = "test" });
        Assert.Equal("2.0.0.0", dn.AppVersion);
    }
}
