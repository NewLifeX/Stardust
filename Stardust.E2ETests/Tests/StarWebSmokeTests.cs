using System.Diagnostics;
using System.Text;
using Microsoft.Playwright;
using Stardust.E2ETests.Fixtures;
using Xunit;
using Xunit.Abstractions;

namespace Stardust.E2ETests.Tests;

/// <summary>StarWeb 全站页面冒烟：登录、逐页访问菜单页面并断言无服务端错误与 JS 崩溃</summary>
[Collection(StarWebCollection.Name)]
public class StarWebSmokeTests : IAsyncLifetime
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

    /// <summary>已知的第三方/外部问题：只做告警，不计入回归失败</summary>
    private static readonly Dictionary<String, String> _knownIssues = new(StringComparer.OrdinalIgnoreCase)
    {
        ["/Cube/Area"] = "百度地图 AK 未授权（getscript 返回 unauth=1），ECharts bmap 抛 getAxesOnZeroOf；属第三方地图密钥失效",
    };

    private readonly StarWebAppFixture _fixture;
    private readonly ITestOutputHelper _output;
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private IBrowserContext? _context;
    private IPage? _page;

    public StarWebSmokeTests(StarWebAppFixture fixture, ITestOutputHelper output)
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
            // 站点按 Accept-Language 协商界面语言，固定浏览器区域语言以保证标题断言的确定性
            Locale = "zh-CN",
        });
        _page = await _context.NewPageAsync();

        var resp = await _page.GotoAsync("/Admin/User/Login", new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 30_000 });
        Assert.Equal(200, resp?.Status);

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

    [Fact(DisplayName = "登录后进入管理后台")]
    public async Task Login_EntersAdmin()
    {
        Assert.Contains("/Admin", _page!.Url, StringComparison.OrdinalIgnoreCase);

        // 站点标题来自程序集标题（当前为英文源文，浏览器标题未走界面语言翻译），故中英文名均视为正确
        var title = await _page.TitleAsync();
        Assert.True(title.Contains("星尘", StringComparison.Ordinal) || title.Contains("Stardust", StringComparison.OrdinalIgnoreCase),
            $"管理后台标题应包含站点名称，实际：{title}");
    }

    [Fact(DisplayName = "未登录访问业务页面被重定向到登录页")]
    public async Task Anonymous_RedirectedToLogin()
    {
        var context = await _browser!.NewContextAsync(new BrowserNewContextOptions { BaseURL = _fixture.BaseUrl });
        var page = await context.NewPageAsync();
        var resp = await page.GotoAsync("/Configs/AppConfig", new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 30_000 });

        var status = resp?.Status ?? 0;
        var redirected = page.Url.Contains("/Admin/User/Login", StringComparison.OrdinalIgnoreCase);
        var denied = status is 401 or 403;

        Assert.True(redirected || denied, $"未登录访问业务页面应跳转登录或被拒绝，实际 status={status} url={page.Url}");
        await context.DisposeAsync();
    }

    [Fact(DisplayName = "全部菜单页面：可访问、无服务端错误、无 JS 崩溃")]
    public async Task AllMenuPages_LoadNormally()
    {
        var menus = _fixture.GetMenuPages();
        _output.WriteLine($"菜单页面数量：{menus.Count}");
        Assert.NotEmpty(menus);

        var results = new List<PageCheckResult>();
        foreach (var menu in menus)
        {
            var result = await CheckPageAsync(menu);
            results.Add(result);
            var flag = result.Ok ? "OK" : result.Known ? "WARN" : "FAIL";
            _output.WriteLine($"[{flag}] {menu.Name} {menu.Url} -> {result.Status} {result.ElapsedMs}ms {(result.Known ? result.KnownNote : result.Detail)}");
        }

        WriteReport(results);

        var failures = results.Where(e => !e.Ok && !e.Known).ToList();
        Assert.True(failures.Count == 0, $"有 {failures.Count}/{results.Count} 个页面异常：{Environment.NewLine}" +
            String.Join(Environment.NewLine, failures.Select(e => $"  {e.Name} {e.Url} -> {e.Status} {e.Detail}")));
    }

    private async Task<PageCheckResult> CheckPageAsync(MenuPage menu)
    {
        var page = _page!;
        var consoleErrors = new List<String>();
        var pageErrors = new List<String>();

        void OnConsole(Object? sender, IConsoleMessage msg)
        {
            if (msg.Type == "error") consoleErrors.Add(msg.Text);
        }

        void OnPageError(Object? sender, String err) => pageErrors.Add(err);

        page.Console += OnConsole;
        page.PageError += OnPageError;

        var sw = Stopwatch.StartNew();
        var status = 0;
        var title = "";
        var detail = "";
        var body = "";
        try
        {
            var resp = await page.GotoAsync(menu.Url, new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 30_000 });
            status = resp?.Status ?? 0;

            try
            {
                await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new PageWaitForLoadStateOptions { Timeout = 5_000 });
            }
            catch (TimeoutException)
            {
                // 长轮询页面不会进入 networkidle，忽略
            }

            title = await page.TitleAsync();
            body = await page.Locator("body").InnerTextAsync();
        }
        catch (Exception ex)
        {
            detail = ex.Message;
        }
        finally
        {
            page.Console -= OnConsole;
            page.PageError -= OnPageError;
            sw.Stop();
        }

        var reasons = new List<String>();
        if (status is 0 or >= 400) reasons.Add($"HTTP {status}");
        if (detail.Length > 0) reasons.Add(detail);
        if (page.Url.Contains("/Admin/User/Login", StringComparison.OrdinalIgnoreCase)) reasons.Add("会话失效被踢回登录页");
        if (pageErrors.Count > 0) reasons.Add($"JS 异常 {pageErrors.Count}：{String.Join(" | ", pageErrors.Take(2))}");
        if (body.Length < 20 && title.Length == 0) reasons.Add("页面空白");

        var marker = _errorMarkers.FirstOrDefault(e => body.Contains(e, StringComparison.OrdinalIgnoreCase));
        if (marker != null) reasons.Add($"响应含错误标记[{marker}]");

        var ok = reasons.Count == 0;
        var known = !ok && _knownIssues.TryGetValue(menu.Url, out var note);
        var knownNote = known ? _knownIssues[menu.Url] : "";
        if (!ok)
        {
            try
            {
                var dir = Path.Combine(_fixture.RepoRoot, "TestResults", "starweb-screenshots");
                Directory.CreateDirectory(dir);
                await page.ScreenshotAsync(new PageScreenshotOptions { Path = Path.Combine(dir, Sanitize(menu.Url) + ".png") });
            }
            catch
            {
                // 截图失败不影响断言
            }
        }

        return new PageCheckResult(menu.Name, menu.Url, status, sw.ElapsedMilliseconds, title, consoleErrors.Count, ok, known, knownNote,
            String.Join("；", reasons));
    }

    private void WriteReport(List<PageCheckResult> results)
    {
        var dir = Path.Combine(_fixture.RepoRoot, "TestResults");
        Directory.CreateDirectory(dir);

        var sb = new StringBuilder();
        sb.AppendLine("# StarWeb 全站页面巡检");
        sb.AppendLine();
        sb.AppendLine($"- 站点：{_fixture.BaseUrl}");
        sb.AppendLine($"- 时间：{DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"- 页面：{results.Count}，通过 {results.Count(e => e.Ok)}，已知问题 {results.Count(e => e.Known)}，失败 {results.Count(e => !e.Ok && !e.Known)}");
        sb.AppendLine();
        sb.AppendLine("| 页面 | 地址 | 状态 | 耗时(ms) | JS错误 | 标题 | 结果 |");
        sb.AppendLine("|---|---|---|---|---|---|---|");
        foreach (var r in results)
        {
            var result = r.Ok ? "✅" : r.Known ? "⚠️ 已知问题：" + r.KnownNote : "❌ " + r.Detail;
            sb.AppendLine($"| {r.Name} | {r.Url} | {r.Status} | {r.ElapsedMs} | {r.ConsoleErrors} | {r.Title} | {result} |");
        }

        File.WriteAllText(Path.Combine(dir, "starweb-pages.md"), sb.ToString(), Encoding.UTF8);
    }

    private static String Sanitize(String url) => url.Trim('/').Replace('/', '_').Replace('?', '_').Replace('&', '_');

    private sealed record PageCheckResult(String Name, String Url, Int32 Status, Int64 ElapsedMs, String Title, Int32 ConsoleErrors, Boolean Ok, Boolean Known, String KnownNote, String Detail);
}
