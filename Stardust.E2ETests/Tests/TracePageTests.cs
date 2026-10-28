using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Playwright;
using Stardust.E2ETests.Fixtures;
using Xunit;
using Xunit.Abstractions;

namespace Stardust.E2ETests.Tests;

/// <summary>调用链页面端到端测试。覆盖链路查询页可加载、无 JS 崩溃、异常编号友好降级（APM-16）</summary>
[Collection(StarWebCollection.Name)]
public class TracePageTests : IAsyncLifetime
{
    private static readonly String[] _errorMarkers =
    [
        "Server Error",
        "服务器错误",
        "An unhandled exception",
        "System.NullReferenceException",
        "System.InvalidOperationException",
        "XCode.Exceptions.XSqlException",
        "SQL logic error",
    ];

    private readonly StarWebAppFixture _fixture;
    private readonly ITestOutputHelper _output;
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private IBrowserContext? _context;
    private IPage? _page;

    public TracePageTests(StarWebAppFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    public async Task InitializeAsync()
    {
        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
        _context = await _browser.NewContextAsync(new BrowserNewContextOptions
        {
            BaseURL = _fixture.BaseUrl,
            IgnoreHTTPSErrors = true,
            ViewportSize = new ViewportSize { Width = 1440, Height = 900 },
            Locale = "zh-CN",
        });
        _page = await _context.NewPageAsync();

        await _page.GotoAsync("/Admin/User/Login", new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 30_000 });
        await _page.FillAsync("#username", _fixture.UserName);
        await _page.FillAsync("#password", _fixture.Password);
        await _page.ClickAsync("#login-btn");
        await _page.WaitForURLAsync(url => !url.Contains("/Admin/User/Login", StringComparison.OrdinalIgnoreCase), new PageWaitForURLOptions { Timeout = 30_000 });
    }

    public async Task DisposeAsync()
    {
        if (_context != null) await _context.DisposeAsync();
        if (_browser != null) await _browser.DisposeAsync();
        _playwright?.Dispose();
    }

    /// <summary>打开指定页面，收集控制台/页面异常与服务端错误标记</summary>
    private async Task<(Int32 Status, String Body, List<String> Errors)> OpenAsync(String url)
    {
        var page = _page!;
        var errors = new List<String>();

        void OnPageError(Object? sender, String err) => errors.Add("JS 异常：" + err);

        page.PageError += OnPageError;
        try
        {
            var resp = await page.GotoAsync(url, new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 30_000 });
            Assert.Equal(200, resp?.Status);

            try
            {
                await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new PageWaitForLoadStateOptions { Timeout = 5_000 });
            }
            catch (TimeoutException)
            {
                // 长轮询页面不会进入 networkidle，忽略
            }

            var body = await page.Locator("body").InnerTextAsync();
            var marker = _errorMarkers.FirstOrDefault(e => body.Contains(e, StringComparison.OrdinalIgnoreCase));
            if (marker != null) errors.Add("响应含错误标记：" + marker);

            // 会话失效会被悠回登录页，这里必须确认仍在目标页面
            if (page.Url.Contains("/Admin/User/Login", StringComparison.OrdinalIgnoreCase)) errors.Add("会话失效被踢回登录页");

            return (resp?.Status ?? 0, body, errors);
        }
        finally
        {
            page.PageError -= OnPageError;
        }
    }

    [Fact(DisplayName = "链路查询页可加载且无 JS 崩溃")]
    public async Task TracePage_Loads()
    {
        var (status, body, errors) = await OpenAsync("/Monitors/Trace");

        _output.WriteLine($"status={status} bodyLength={body.Length} errors={String.Join(" | ", errors)}");

        Assert.Equal(200, status);
        Assert.Contains("全链路追踪", body);
        Assert.Empty(errors);

        // 自定义页面不会自动带标题，控制器需显式设置，否则浏览器标签页为空
        var title = await _page!.TitleAsync();
        Assert.Contains("链路查询", title);
    }

    [Fact(DisplayName = "不存在的追踪编号给出降级提示而不崩溃")]
    public async Task TracePage_UnknownTraceId_Degrades()
    {
        var (status, body, errors) = await OpenAsync("/Monitors/Trace?id=not-exists-" + Guid.NewGuid().ToString("n")[..8]);

        _output.WriteLine($"status={status} bodyLength={body.Length} errors={String.Join(" | ", errors)}");

        Assert.Equal(200, status);

        // 无数据时页面应展示查询摘要（0 次）而不是报错
        Assert.Contains("查询", body);
        Assert.Contains("0次", body);
        Assert.Empty(errors);
    }
}
