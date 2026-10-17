using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using NewLife;
using NewLife.Remoting;
using NewLife.Remoting.Models;
using Stardust.Data.Deployment;
using Stardust.Data.Nodes;
using Stardust.Server;
using Xunit;

namespace ServerTest.Controllers;

/// <summary>Deploy/UploadBuildFile 上传授权安全测试。验证任意已注册节点不能向无关部署集投递构建产物（安全扫描 V-4）</summary>
[Collection(StarTestServerCollection.Name)]
public class DeployUploadSecurityTests : StarTestBase
{
    public DeployUploadSecurityTests(StarTestServer fixture) : base(fixture) { }

    private async Task<(String token, Node node)> LoginNodeAsync()
    {
        var client = _server.CreateClient();
        var rs = await client.PostAsync<LoginResponse>("node/login", new
        {
            code = "",
            node = new
            {
                MachineName = "test_upload_sec",
                macs = "xxyyzz" + Guid.NewGuid().ToString("N")[..6],
            },
        });
        Assert.NotNull(rs?.Token);
        var node = Node.FindByCode(rs?.Code);
        Assert.NotNull(node);
        return (rs.Token, node);
    }

    private static MultipartFormDataContent BuildContent()
    {
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(new Byte[] { 0x50, 0x4B, 0x05, 0x06, 1, 2, 3, 4 });
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/zip");
        content.Add(fileContent, "file", "test.zip");
        return content;
    }

    private async Task<String> UploadAsync(String token, String deployName, String version)
    {
        var client = _server.CreateClient();
        client.DefaultRequestHeaders.Add("X-Token", token);
        var response = await client.PostAsync($"/Deploy/UploadBuildFile?deployName={Uri.EscapeDataString(deployName)}&version={Uri.EscapeDataString(version)}", BuildContent());
        return await response.Content.ReadAsStringAsync();
    }

    private static AppDeploy NewDeploy(String name)
    {
        var app = new AppDeploy { Name = name, Enable = true };
        app.Insert();
        return app;
    }

    [Fact(DisplayName = "未关联发布集的任意节点上传构建产物被拒绝")]
    public async Task UnrelatedNode_Upload_Rejected()
    {
        var deployName = "v4u_" + Guid.NewGuid().ToString("N")[..8];
        var app = NewDeploy(deployName);
        try
        {
            var (token, node) = await LoginNodeAsync();

            var body = await UploadAsync(token, deployName, "v1.0.0");

            Assert.Contains("\"code\"", body);
            Assert.Contains("禁止上传", body);
        }
        finally
        {
            app.Delete();
        }
    }

    [Fact(DisplayName = "编译节点上传构建产物成功（回归保护）")]
    public async Task BuildNode_Upload_Ok()
    {
        var deployName = "v4b_" + Guid.NewGuid().ToString("N")[..8];
        var version = "v" + DateTime.Now.ToString("yyyyMMddHHmmss");
        var app = NewDeploy(deployName);
        try
        {
            var (token, node) = await LoginNodeAsync();
            new AppBuildNode { DeployId = app.Id, NodeId = node.ID, Enable = true }.Insert();

            var body = await UploadAsync(token, deployName, version);

            var ver = AppDeployVersion.FindByDeployIdAndVersion(app.Id, version);
            Assert.NotNull(ver);
            Assert.Equal(version, ver.Version);
        }
        finally
        {
            foreach (var v in AppDeployVersion.FindAllByDeployId(app.Id)) v.Delete();
            foreach (var b in AppBuildNode.FindAll(AppBuildNode._.DeployId == app.Id)) b.Delete();
            app.Delete();
        }
    }

    [Fact(DisplayName = "部署节点上传构建产物成功（兼容回归保护）")]
    public async Task DeployNode_Upload_Ok()
    {
        var deployName = "v4d_" + Guid.NewGuid().ToString("N")[..8];
        var version = "v" + DateTime.Now.ToString("yyyyMMddHHmmss");
        var app = NewDeploy(deployName);
        try
        {
            var (token, node) = await LoginNodeAsync();
            new AppDeployNode { DeployId = app.Id, NodeId = node.ID, Enable = true }.Insert();

            var body = await UploadAsync(token, deployName, version);

            var ver = AppDeployVersion.FindByDeployIdAndVersion(app.Id, version);
            Assert.NotNull(ver);
            Assert.Equal(version, ver.Version);
        }
        finally
        {
            foreach (var v in AppDeployVersion.FindAllByDeployId(app.Id)) v.Delete();
            foreach (var d in AppDeployNode.FindAllByDeployId(app.Id)) d.Delete();
            app.Delete();
        }
    }
}
