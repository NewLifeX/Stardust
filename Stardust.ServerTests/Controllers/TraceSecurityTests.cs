using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using NewLife;
using NewLife.Remoting;
using NewLife.Remoting.Models;
using NewLife.Security;
using Stardust.Data;
using Stardust.Data.Monitors;
using Stardust.Server;
using Xunit;

namespace ServerTest.Controllers;

/// <summary>埋点上报鉴权安全测试。验证匿名上报默认被拒、令牌不能冒名其它应用灌数据（安全扫描 V-9）</summary>
[Collection(StarTestServerCollection.Name)]
public class TraceSecurityTests : StarTestBase
{
    public TraceSecurityTests(StarTestServer fixture) : base(fixture) { }

    private async Task<String> AppLoginTokenAsync(String appId, String secret)
    {
        var client = _server.CreateClient();
        var rs = await client.PostAsync<LoginResponse>("app/login", new { AppId = appId, Secret = secret });
        Assert.NotNull(rs?.Token);
        return rs.Token;
    }

    private async Task<String> ReportAsync(String appId, String token)
    {
        var client = _server.CreateClient();
        if (!token.IsNullOrEmpty())
            client.DefaultRequestHeaders.Add("X-Token", token);

        var model = new { AppId = appId, ClientId = "test-client" };
        var response = await client.PostAsync("/Trace/Report", JsonContent.Create(model));
        return await response.Content.ReadAsStringAsync();
    }

    private App CreateApp(String name, String secret)
    {
        var app = new App { Name = name, Enable = true };
        if (!secret.IsNullOrEmpty()) app.Secret = secret;
        app.Insert();

        // 埋点上报需要已存在的 AppTracer（Enable=true）
        new AppTracer { Name = name, AppId = app.Id, Enable = true }.Insert();
        return app;
    }

    [Fact(DisplayName = "匿名埋点上报默认被拒绝")]
    public async Task AnonymousReport_Rejected()
    {
        var app = CreateApp("trx_" + Rand.NextString(8), "trx-secret");
        try
        {
            var body = await ReportAsync(app.Name, "");

            Assert.Contains("\"code\"", body);
            Assert.Contains("匿名", body);
        }
        finally
        {
            AppTracer.FindByName(app.Name)?.Delete();
            app.Delete();
        }
    }

    [Fact(DisplayName = "应用令牌不能冒名其它应用上报")]
    public async Task Token_CannotReportAsOtherApp()
    {
        var a = CreateApp("trx_" + Rand.NextString(8), "trx-secret-a");
        var b = CreateApp("try_" + Rand.NextString(8), "trx-secret-b");
        try
        {
            var token = await AppLoginTokenAsync(a.Name, "trx-secret-a");

            var body = await ReportAsync(b.Name, token);

            Assert.Contains("\"code\"", body);
            Assert.Contains("不一致", body);
        }
        finally
        {
            AppTracer.FindByName(a.Name)?.Delete();
            AppTracer.FindByName(b.Name)?.Delete();
            a.Delete();
            b.Delete();
        }
    }

    [Fact(DisplayName = "应用令牌上报自身埋点正常（回归保护）")]
    public async Task Token_ReportSelf_Ok()
    {
        var app = CreateApp("trz_" + Rand.NextString(8), "trz-secret");
        try
        {
            var token = await AppLoginTokenAsync(app.Name, "trz-secret");

            var body = await ReportAsync(app.Name, token);

            Assert.DoesNotContain("匿名", body);
            Assert.DoesNotContain("不一致", body);
        }
        finally
        {
            AppTracer.FindByName(app.Name)?.Delete();
            app.Delete();
        }
    }

    [Fact(DisplayName = "注册应用改名后，监控身份显示名跟随同步")]
    public async Task AppRename_TracerDisplayNameFollows()
    {
        var name = "trf_" + Rand.NextString(8);
        var app = CreateApp(name, "trf-secret");
        try
        {
            var token = await AppLoginTokenAsync(name, "trf-secret");

            // 注册应用设置显示名
            app.DisplayName = "旧名字";
            app.Update();

            await ReportAsync(name, token);
            Assert.Equal("旧名字", AppTracer.FindByName(name)?.DisplayName);

            // 注册应用改名后再次上报，监控身份应跟随新名字
            app.DisplayName = "新名字";
            app.Update();

            await ReportAsync(name, token);
            Assert.Equal("新名字", AppTracer.FindByName(name)?.DisplayName);
        }
        finally
        {
            AppTracer.FindByName(name)?.Delete();
            app.Delete();
        }
    }

    [Fact(DisplayName = "SetHotApp 公开接口已下线")]
    public async Task SetHotApp_EndpointRemoved()
    {
        // 热门应用通知已改为跨应用事件总线，原公开接口删除后不应再可访问
        var client = _server.CreateClient();

        var response = await client.PostAsync("/Trace/SetHotApp", JsonContent.Create(1));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
