using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Stardust.E2ETests.Fixtures;
using Xunit;

namespace Stardust.E2ETests.Tests;

/// <summary>匿名访问测试。后台自定义页面必须要求登录，只有明确标注 AllowAnonymous 的页面允许匿名</summary>
/// <remarks>自定义页面不会自动继承实体控制器的鉴权，漏加特性即对匿名用户敞开</remarks>
[Collection(StarWebCollection.Name)]
public class AnonymousAccessTests(StarWebAppFixture fixture)
{
    /// <summary>匿名请求指定地址，不自动跟随重定向</summary>
    private async Task<HttpStatusCode> GetAsync(String url, String? accept = null)
    {
        using var handler = new HttpClientHandler { AllowAutoRedirect = false };
        using var http = new HttpClient(handler) { BaseAddress = new Uri(fixture.BaseUrl) };
        if (!String.IsNullOrEmpty(accept)) http.DefaultRequestHeaders.TryAddWithoutValidation("Accept", accept);

        var rs = await http.GetAsync(url);
        return rs.StatusCode;
    }

    [Theory(DisplayName = "后台自定义页面匿名访问被重定向到登录页")]
    [InlineData("/Monitors/Trace")]
    [InlineData("/Nodes/AgentDeploy")]
    [InlineData("/Dashboard")]
    [InlineData("/Dashboard/System")]
    public async Task AdminPages_RequireLogin(String url)
    {
        var status = await GetAsync(url);

        // 未登录时魔方跳转登录页；若返回 200 说明该页面缺少鉴权特性
        Assert.True(status is HttpStatusCode.Redirect or HttpStatusCode.Found or HttpStatusCode.Unauthorized,
            $"{url} 对匿名用户返回 {(Int32)status}，应跳转登录");
    }

    [Fact(DisplayName = "看板数据接口匿名访问返回401")]
    public async Task DashboardData_RequiresLogin()
    {
        var status = await GetAsync("/Dashboard/Data?page=system&range=24h&id=0", "application/json");

        // 轮询接口不能把登录页 HTML 返回给前端，必须明确 401
        Assert.Equal(HttpStatusCode.Unauthorized, status);
    }

    [Fact(DisplayName = "代理安装页面按设计允许匿名访问")]
    public async Task AgentInstallPage_IsPublic()
    {
        var status = await GetAsync("/Agent");

        Assert.Equal(HttpStatusCode.OK, status);
    }

    [Fact(DisplayName = "调用链页面默认要求登录")]
    public async Task TracePage_RequiresLogin()
    {
        var status = await GetAsync("/trace?id=" + Guid.NewGuid().ToString("n"));

        // 默认配置不允许匿名，未登录应跳转登录页；返回 200 说明访问判定被绕过
        Assert.True(status is HttpStatusCode.Redirect or HttpStatusCode.Found or HttpStatusCode.Unauthorized,
            $"/trace 对匿名用户返回 {(Int32)status}，应跳转登录");
    }
}
