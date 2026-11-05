using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Stardust.WebApiTests.Fixtures;
using Xunit;
using Xunit.Abstractions;

namespace Stardust.WebApiTests.Spa;

/// <summary>
/// SPA 托管与回退测试：验证内嵌的新控制台前端在根路径与 History 深链可用，
/// 且 /api 机器端点的 404 不被 SPA 回退吞掉（魔方皮肤系统 MapFallback("/api/{**path}", 404) 保护）。
/// </summary>
/// <remarks>
/// 前端产物在编译期嵌入 Stardust.WebApi 程序集（csproj 嵌 wwwroot\**\*，由 pnpm build:all 同步 dist 生成），
/// 运行时由魔方皮肤系统托管（ApiSpaSkin : ICubeSkin 自动发现）。
/// 产物缺失（未执行 pnpm build:all 的纯后端构建/CI）时用例直接返回，不阻塞门禁。
/// 回退端点默认 {*path:nonfile} 约束（实测）：无扩展名未匹配路径回退 index.html（no-cache）；
/// 带扩展名的缺失资源保持 404、POST UI 路径返回 405——机器语义不受影响，此处一并锁定。
/// </remarks>
[Collection(WebApiCollection.Name)]
public class SpaFallbackTests
{
    private static Regex _spaEmbedded = new("^Stardust\\.Web\\.wwwroot\\.index\\.html$", RegexOptions.IgnoreCase);

    private readonly WebApiTestFactory _factory;
    private readonly ITestOutputHelper _output;

    public SpaFallbackTests(WebApiTestFactory factory, ITestOutputHelper output)
    {
        _factory = factory;
        _output = output;
    }

    /// <summary>当前是否内嵌了前端入口。缺失时跳过回退类断言，保持纯后端构建可跑。</summary>
    private Boolean SpaEmbedded => typeof(Program).Assembly.GetManifestResourceNames().Any(e => _spaEmbedded.IsMatch(e));

    /// <summary>带 Accept 头的裸 HttpClient（不走 ApiClient 信封封装，需要原始状态码与响应体）。</summary>
    private HttpClient CreateClient(String accept)
    {
        var client = _factory.CreateClient();
        if (!String.IsNullOrEmpty(accept)) client.DefaultRequestHeaders.Accept.ParseAdd(accept);
        return client;
    }

    private Boolean SkipWhenNoSpa()
    {
        if (SpaEmbedded) return false;

        _output.WriteLine("跳过：程序集未内嵌前端（未执行 pnpm build:all），纯后端构建不托管 SPA");
        return true;
    }

    [Fact]
    public async Task GetRoot_ServesEmbeddedIndexHtml()
    {
        if (SkipWhenNoSpa()) return;

        using var client = CreateClient("text/html");
        var resp = await client.GetAsync("/");
        var body = await resp.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.Contains("text/html", resp.Content.Headers.ContentType!.MediaType, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("id=\"app\"", body);
        // 构建期注入的 BUILD_ 占位符必须被宿主替换为空值（同源请求），否则前端会请求伪造主机
        Assert.DoesNotContain("BUILD_REQUEST_BASE_URL", body);
        // 入口页不缓存：前端发布后刷新即可加载新版本（与魔方皮肤行为一致）
        Assert.True(resp.Headers.CacheControl!.NoCache == true, "index.html 应带 Cache-Control: no-cache");
    }

    [Fact]
    public async Task GetDeepLink_FallsBackToIndexHtml()
    {
        if (SkipWhenNoSpa()) return;

        // History 路由深链：服务端没有 /app/overview/9 这个物理资源，应回退到 SPA 入口由前端路由接管
        using var client = CreateClient("text/html");
        var resp = await client.GetAsync("/app/overview/9");
        var body = await resp.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.Contains("text/html", resp.Content.Headers.ContentType!.MediaType, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("id=\"app\"", body);
    }

    [Fact]
    public async Task GetIndexHtml_ServedDirectly()
    {
        if (SkipWhenNoSpa()) return;

        // 皮肤静态文件提供者带占位符补丁：直出入口页同样不能携带 BUILD_REQUEST_BASE_URL 字面量
        using var client = CreateClient("text/html");
        var resp = await client.GetAsync("/index.html");
        var body = await resp.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.Contains("text/html", resp.Content.Headers.ContentType!.MediaType, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("BUILD_REQUEST_BASE_URL", body);
    }

    [Fact]
    public async Task GetUnknownApi_Keeps404()
    {
        if (SkipWhenNoSpa()) return;

        // 即使浏览器导航（Accept 含 text/html），/api 未匹配也必须保持 404，不能被 SPA 回退成 index.html
        using var client = CreateClient("text/html");
        var resp = await client.GetAsync("/api/__NoSuchController__/__NoSuchAction__");
        var body = await resp.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
        Assert.DoesNotContain("id=\"app\"", body);
    }

    [Fact]
    public async Task PostUnknownUiPath_Returns405()
    {
        if (SkipWhenNoSpa()) return;

        // 回退端点不响应非 GET/HEAD：POST 打到未匹配 UI 路径得到 405（无 HTML 泄漏），Webhook 等机器请求打错地址不会误吞
        using var client = CreateClient("text/html");
        var resp = await client.PostAsync("/app/overview/9", new StringContent("{}", Encoding.UTF8, "application/json"));
        var body = await resp.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.MethodNotAllowed, resp.StatusCode);
        Assert.DoesNotContain("id=\"app\"", body);
    }

    [Fact]
    public async Task PostUnknownApi_Keeps404()
    {
        if (SkipWhenNoSpa()) return;

        // POST 未匹配 /api 路径受 /api 回退保护：必须拿到 404 而不是 HTML
        using var client = CreateClient("text/html");
        var resp = await client.PostAsync("/api/__NoSuchController__/__NoSuchAction__", new StringContent("{}", Encoding.UTF8, "application/json"));
        var body = await resp.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
        Assert.DoesNotContain("id=\"app\"", body);
    }

    [Fact]
    public async Task GetMissingAsset_Keeps404()
    {
        if (SkipWhenNoSpa()) return;

        // 回退端点 {*path:nonfile} 约束（实测）：带扩展名的缺失资源不回退，保持 404——
        // 避免把 HTML 当脚本返回造成语法错误（与自研中间件的 Accept 过滤等效）
        using var client = CreateClient("*/*");
        var resp = await client.GetAsync("/assets/__missing_hash__/index.js");
        var body = await resp.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
        Assert.DoesNotContain("id=\"app\"", body);
    }
}
