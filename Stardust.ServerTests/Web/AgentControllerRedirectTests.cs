extern alias web;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace ServerTest.Web;

/// <summary>StarWeb 代理安装页跳转测试。
/// 语义：配置了 StarServer 安装页地址后，本站页面跳转到 StarServer 的安装引导页；
/// 未配置时保持原有页面，JSON 请求始终维持旧接口以兼容存量客户端。</summary>
public class AgentControllerRedirectTests
{
    private static web::Stardust.Web.Controllers.AgentController Create(String agentServerUrl, String path = "/Agent")
    {
        var controller = new web::Stardust.Web.Controllers.AgentController(new web::Stardust.Server.StarServerSetting { AgentServerUrl = agentServerUrl });

        var ctx = new DefaultHttpContext();
        ctx.Request.Scheme = "http";
        ctx.Request.Host = new HostString("web.example.com", 6680);
        ctx.Request.Path = path;
        controller.ControllerContext = new ControllerContext { HttpContext = ctx };

        return controller;
    }

    [Fact]
    [Trait("Category", "Install")]
    public void Configured_RedirectsToInstallPage()
    {
        var result = Create("https://star.example.com:6600").Index();

        var redirect = Assert.IsType<RedirectResult>(result);
        Assert.Equal("https://star.example.com:6600/", redirect.Url);
    }

    /// <summary>地址末尾带斜杠时不应产生双斜杠。</summary>
    [Fact]
    [Trait("Category", "Install")]
    public void ConfiguredWithTrailingSlash_RedirectsSafely()
    {
        var result = Create("https://star.example.com:6600/").Index();

        var redirect = Assert.IsType<RedirectResult>(result);
        Assert.Equal("https://star.example.com:6600/", redirect.Url);
    }

    [Fact]
    [Trait("Category", "Install")]
    public void NotConfigured_ReturnsLocalPage()
    {
        var result = Create("").Index();

        Assert.IsType<ViewResult>(result);
    }

    /// <summary>JSON 请求保持原接口，避免打断仍在调用 /Agent?format=json 的客户端。</summary>
    [Fact]
    [Trait("Category", "Install")]
    public void JsonRequest_KeepsLegacyEndpoint()
    {
        var controller = Create("https://star.example.com:6600");
        controller.Request.QueryString = new QueryString("?format=json");

        var result = controller.Index();

        Assert.IsType<JsonResult>(result);
    }

    /// <summary>配置的成本站地址时不跳转，避免自跳转死循环。</summary>
    [Fact]
    [Trait("Category", "Install")]
    public void SameOrigin_DoesNotRedirect()
    {
        var result = Create("http://web.example.com:6680").Index();

        Assert.IsType<ViewResult>(result);
    }
}
