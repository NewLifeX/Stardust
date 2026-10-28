using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Playwright;
using Stardust.E2ETests.Fixtures;
using Xunit;
using Xunit.Abstractions;

namespace Stardust.E2ETests.Tests;

/// <summary>应用日志查询页端到端测试。覆盖带条件查询的页面渲染与空结果降级（LOG-2）</summary>
[Collection(StarWebCollection.Name)]
public class AppClientLogPageTests : IAsyncLifetime
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

    public AppClientLogPageTests(StarWebAppFixture fixture, ITestOutputHelper output)
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

    [Fact(DisplayName = "按条件查询应用日志：页面正常渲染且无服务端错误")]
    public async Task Query_WithFilter_Renders()
    {
        var page = _page!;
        var errors = new List<String>();
        void OnPageError(Object? sender, String err) => errors.Add("JS 异常：" + err);

        page.PageError += OnPageError;
        try
        {
            // 指定一个不存在的应用编号与历史日期区间，走真实查询分支并返回空集
            var url = "/Registry/AppClientLog?appId=999999&dtStart=2026-10-01&dtEnd=2026-10-02";
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

            Assert.DoesNotContain("/Admin/User/Login", page.Url, StringComparison.OrdinalIgnoreCase);

            var body = await page.Locator("body").InnerTextAsync();
            _output.WriteLine($"status={resp?.Status} bodyLength={body.Length} errors={String.Join(" | ", errors)}");

            var marker = _errorMarkers.FirstOrDefault(e => body.Contains(e, StringComparison.OrdinalIgnoreCase));
            Assert.Null(marker);

            // 查询页应渲染出表格（含表头）或明确的空数据提示
            var hasTable = await page.Locator("table").CountAsync() > 0;
            var hasEmptyHint = body.Contains("暂无") || body.Contains("没有") || body.Contains("0 条");
            Assert.True(hasTable || hasEmptyHint, "查询结果区应渲染表格或空数据提示");

            Assert.Empty(errors);
        }
        finally
        {
            page.PageError -= OnPageError;
        }
    }
}
