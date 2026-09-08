using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NewLife;
using Stardust.Data.Deployment;
using Stardust.Server;
using Stardust.Server.Services;
using Xunit;

namespace ServerTest.Controllers;

/// <summary>附件下载鉴权集成测试（安全V-11）。验证 ValidateAttachment 开关与短时下载令牌</summary>
public class CubeAttachmentSecurityTests
{
    private readonly TestServer _server;

    public CubeAttachmentSecurityTests()
    {
#pragma warning disable CS0618, ASPDEPR008
        _server = new TestServer(WebHost.CreateDefaultBuilder()
            .UseStartup<Startup>());
#pragma warning restore CS0618, ASPDEPR008
    }

    private async Task<String> DownloadAsync(Int64 id, String token)
    {
        var client = _server.CreateClient();
        var url = $"/cube/file?id={id}";
        if (!token.IsNullOrEmpty()) url += $"&token={token}";
        var response = await client.GetAsync(url);
        return await response.Content.ReadAsStringAsync();
    }

    private Attachment CreateAtt()
    {
        var att = new Attachment
        {
            FileName = "test.zip",
            ContentType = "application/zip",
            Category = "AppDeploy",
            Enable = true,
        };
        att.Insert();
        return att;
    }

    [Fact(DisplayName = "默认未开启校验时附件下载不受令牌限制（回归）")]
    public async Task Default_NoValidate_NotBlocked()
    {
        var att = CreateAtt();
        try
        {
            var body = await DownloadAsync(att.Id, "");

            // 校验未开启：请求到达文件查找阶段（文件缺失返回404而非401）
            Assert.DoesNotContain("未授权", body);
        }
        finally
        {
            att.Delete();
        }
    }

    [Fact(DisplayName = "开启校验后无令牌下载被拒，携带有效令牌放行")]
    public async Task ValidateOn_TokenRequired()
    {
        var set = StarServerSetting.Current;
        var old = set.ValidateAttachment;
        set.ValidateAttachment = true;
        set.Save();

        var att = CreateAtt();
        try
        {
            // 无令牌：拒绝
            var body = await DownloadAsync(att.Id, "");
            Assert.Contains("未授权", body);

            // 有效令牌：通过校验（文件缺失后续走404）
            var token = _server.Services.GetRequiredService<DownloadTokenService>().Issue(att.Id);
            var body2 = await DownloadAsync(att.Id, token);
            Assert.DoesNotContain("未授权", body2);

            // 无效令牌：拒绝
            var body3 = await DownloadAsync(att.Id, "bad-token");
            Assert.Contains("未授权", body3);
        }
        finally
        {
            att.Delete();
            set.ValidateAttachment = old;
            set.Save();
        }
    }

    [Fact(DisplayName = "开启校验后公开分类附件可匿名下载（回归）")]
    public async Task ValidateOn_PublicCategory_Allowed()
    {
        var set = StarServerSetting.Current;
        var old = set.ValidateAttachment;
        var oldCats = set.PublicAttachmentCategories;
        set.ValidateAttachment = true;
        set.PublicAttachmentCategories = "AppDeploy,Avatar";
        set.Save();

        var att = CreateAtt();
        try
        {
            var body = await DownloadAsync(att.Id, "");
            Assert.DoesNotContain("未授权", body);
        }
        finally
        {
            att.Delete();
            set.ValidateAttachment = old;
            set.PublicAttachmentCategories = oldCats;
            set.Save();
        }
    }
}
