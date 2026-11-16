using System.Runtime.CompilerServices;
using Stardust.Data.Deployment;
using Stardust.Data.Nodes;
using Stardust.Models;
using Stardust.WebApiTests.Fixtures;
using Xunit;

namespace Stardust.WebApiTests.Deployment;

/// <summary>
/// install 下发回填部署版本——WebApi 宿主镜像测试（方案C 补齐 2026-11-16）。
/// 方案C 原修复（c471017d）只落了 MVC 宿主（Stardust.Web/Services/DeployService），
/// WebApi 宿主（线上 6681）长期缺席，镜像断言取自 ServerTests/InstallVersionBackfillTests 的 Web 侧三用例；
/// 因 ServerTests 不引用 WebApi 工程（同程序集身份 NewLife.Cube 冲突，见 ServerTest.csproj 注释），
/// 本文件放 WebApiTests 工程直接引用被测宿主。服务实例经 GetUninitializedObject 创建，
/// Install 不依赖注入字段（starFactory/tracer）。版本匹配共享实现已下沉 AppDeploy.GetDeployVersion，
/// 本文件只测回填接线与 Web 宿主同口径。
/// </summary>
[Collection(WebApiCollection.Name)]
public class InstallVersionBackfillApiTests
{
    /// <summary>创建只用于 Install 回填的服务实例。Install 不依赖注入字段</summary>
    private static Stardust.Web.Services.DeployService CreateApiDeployService() =>
        (Stardust.Web.Services.DeployService)RuntimeHelpers.GetUninitializedObject(typeof(Stardust.Web.Services.DeployService));

    private static Node CreateNode(String tag) => new()
    {
        Code = tag + "-code",
        Name = tag + "-nd",
        Enable = true,
        OSKind = OSKinds.Ubuntu,
        Architecture = "x64",
    };

    [Fact(DisplayName = "WebApi install 回填：多版本部署集按平台匹配版本回填节点版本（镜像方案C）")]
    public void ApiInstall_MultiVersion_BackfillsMatchedVersion()
    {
        var tag = "ibk1-" + Guid.NewGuid().ToString("N")[..8];
        var app = new AppDeploy { Name = tag, AppId = 0, ProjectId = 0, Category = "unit", Enable = true, MultiVersion = true };
        app.Insert();
        var node = CreateNode(tag);
        node.Insert();

        var win = new AppDeployVersion { DeployId = app.Id, Version = "1.0.0", OS = OSKind.Windows, Arch = CpuArch.X64, Enable = true };
        win.Insert();
        var linux = new AppDeployVersion { DeployId = app.Id, Version = "1.0.1", OS = OSKind.Linux, Arch = CpuArch.X64, Enable = true };
        linux.Insert();

        var dn = new AppDeployNode { DeployId = app.Id, NodeId = node.ID, DeployName = tag + "-dn", Enable = true };
        try
        {
            dn.Insert();

            CreateApiDeployService().Install(dn);

            // Ubuntu 节点应拿到 Linux 包的版本串，而不是部署集/别的包
            Assert.True(dn.Enable);
            Assert.Equal("1.0.1", dn.Version);
        }
        finally
        {
            dn.Delete();
            win.Delete();
            linux.Delete();
            node.Delete();
            app.Delete();
        }
    }

    [Fact(DisplayName = "WebApi install 回填：版本表无匹配行时兜底部署集版本（镜像方案C）")]
    public void ApiInstall_NoMatchedVersion_FallsBackToAppVersion()
    {
        var tag = "ibk2-" + Guid.NewGuid().ToString("N")[..8];
        var app = new AppDeploy { Name = tag, AppId = 0, ProjectId = 0, Category = "unit", Enable = true, MultiVersion = true, Version = "2.0.0" };
        app.Insert();
        var node = CreateNode(tag);
        node.Insert();

        var dn = new AppDeployNode { DeployId = app.Id, NodeId = node.ID, DeployName = tag + "-dn", Enable = true };
        try
        {
            dn.Insert();

            CreateApiDeployService().Install(dn);

            // 版本表无匹配行，兜底部署集当前版本（与服务端 BuildDeployInfo 无条件 app.Version 同一口径）
            Assert.Equal("2.0.0", dn.Version);
        }
        finally
        {
            dn.Delete();
            node.Delete();
            app.Delete();
        }
    }

    [Fact(DisplayName = "WebApi install 回填：部署集无版本时不写入保持原值（镜像方案C）")]
    public void ApiInstall_NoVersionAnywhere_KeepsOriginalValue()
    {
        var tag = "ibk3-" + Guid.NewGuid().ToString("N")[..8];
        var app = new AppDeploy { Name = tag, AppId = 0, ProjectId = 0, Category = "unit", Enable = true, MultiVersion = false };
        app.Insert();
        var node = CreateNode(tag);
        node.Insert();

        var dn = new AppDeployNode { DeployId = app.Id, NodeId = node.ID, DeployName = tag + "-dn", Enable = true, Version = "keep" };
        try
        {
            dn.Insert();

            CreateApiDeployService().Install(dn);

            Assert.True(dn.Enable);
            Assert.Equal("keep", dn.Version);
        }
        finally
        {
            dn.Delete();
            node.Delete();
            app.Delete();
        }
    }
}
