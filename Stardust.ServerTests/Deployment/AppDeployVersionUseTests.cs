extern alias web;

using System;
using System.Threading.Tasks;
using NewLife.Security;
using Stardust.Data.Deployment;
using Stardust.Data.Nodes;
using Xunit;
using WebDeployService = web::Stardust.Web.Services.DeployService;

namespace ServerTest.Deployment;

/// <summary>使用版本只有一份实现：实体改版本号，宿主服务负责自动发布。</summary>
public class AppDeployVersionUseTests
{
    [Fact(DisplayName = "使用版本：写入部署集当前版本号")]
    public void UseAsCurrent_UpdatesDeployVersion()
    {
        var app = new AppDeploy { Name = "use_" + Rand.NextString(6), Enable = true, Version = "0" };
        app.Insert();
        var ver = new AppDeployVersion
        {
            DeployId = app.Id,
            Version = "1.2.3",
            Url = "http://test/app.zip",
            Enable = true,
        };
        ver.Insert();

        try
        {
            var current = ver.UseAsCurrent();
            Assert.Equal("1.2.3", current.Version);
            Assert.Equal("1.2.3", AppDeploy.FindById(app.Id).Version);
        }
        finally
        {
            ver.Delete();
            app.Delete();
        }
    }

    [Fact(DisplayName = "使用版本：未启用或没有包地址时拒绝")]
    public void UseAsCurrent_RejectsDisabledOrMissingPackage()
    {
        var app = new AppDeploy { Name = "use_" + Rand.NextString(6), Enable = true };
        app.Insert();
        var disabled = new AppDeployVersion { DeployId = app.Id, Version = "1.0.0", Url = "http://test/a.zip", Enable = false };
        var noUrl = new AppDeployVersion { DeployId = app.Id, Version = "1.0.1", Enable = true };
        disabled.Insert();
        noUrl.Insert();

        try
        {
            var disabledEx = Assert.Throws<Exception>(() => disabled.UseAsCurrent());
            Assert.True(disabledEx.Message.Contains("not enabled", StringComparison.OrdinalIgnoreCase) || disabledEx.Message.Contains("未启用"), disabledEx.Message);
            var urlEx = Assert.Throws<Exception>(() => noUrl.UseAsCurrent());
            Assert.True(urlEx.Message.Contains("does not exist", StringComparison.OrdinalIgnoreCase) || urlEx.Message.Contains("不存在"), urlEx.Message);
        }
        finally
        {
            disabled.Delete();
            noUrl.Delete();
            app.Delete();
        }
    }

    [Fact(DisplayName = "使用版本：只切版本时不向启用节点下发安装")]
    public async Task UseVersion_SkipPublish_DoesNotDispatch()
    {
        var app = new AppDeploy { Name = "use_" + Rand.NextString(6), Enable = true, AutoPublish = true, Version = "0" };
        app.Insert();
        var node = new Node { Code = "nd_" + Rand.NextString(8), Name = "use-node", Enable = true };
        node.Insert();
        var deployNode = new AppDeployNode { DeployId = app.Id, NodeId = node.ID, Enable = true };
        deployNode.Insert();
        var ver = new AppDeployVersion
        {
            DeployId = app.Id,
            Version = "2.0.0",
            Url = "http://test/app.zip",
            Enable = true,
        };
        ver.Insert();

        try
        {
            var svc = new WebDeployService(null, null);
            var missing = await Assert.ThrowsAsync<Exception>(() => svc.UseVersion(0, "127.0.0.1"));
            Assert.True(missing.Message.Contains("not found", StringComparison.OrdinalIgnoreCase) || missing.Message.Contains("找不到"), missing.Message);

            var message = await svc.UseVersion(ver.Id, "127.0.0.1", publishEnabledNodes: false);
            Assert.Equal("Version switched; no nodes were dispatched", message);
            Assert.Equal("2.0.0", AppDeploy.FindById(app.Id).Version);

            await Assert.ThrowsAnyAsync<Exception>(() => svc.UseVersion(ver.Id, "127.0.0.1"));
        }
        finally
        {
            deployNode.Delete();
            ver.Delete();
            app.Delete();
            node.Delete();
        }
    }
}
