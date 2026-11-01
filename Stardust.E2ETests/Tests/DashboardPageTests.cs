using System.Net;
using Microsoft.Playwright;
using Stardust.E2ETests.Fixtures;
using Xunit;
using Xunit.Abstractions;

namespace Stardust.E2ETests.Tests;

/// <summary>看板中心端到端测试。覆盖五个看板加载、JSON 数据接口与主题切换</summary>
[Collection(StarWebCollection.Name)]
public class DashboardPageTests : IAsyncLifetime
{
    private static readonly (String Url, String Page)[] _pages =
    [
        ("/", "system"),
        ("/Dashboard/System", "system"),
        ("/Dashboard/Projects", "projects"),
        ("/Dashboard/Apps", "apps"),
        ("/Dashboard/Nodes", "nodes"),
        ("/Dashboard/Wallboard", "wallboard"),
    ];

    private readonly StarWebAppFixture _fixture;
    private readonly ITestOutputHelper _output;
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private IBrowserContext? _context;
    private IPage? _page;

    public DashboardPageTests(StarWebAppFixture fixture, ITestOutputHelper output)
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
            ViewportSize = new ViewportSize { Width = 1600, Height = 1000 },
            Locale = "zh-CN",
        });
        _page = await _context.NewPageAsync();

        await _page.GotoAsync("/Admin/User/Login", new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 30_000 });
        await _page.FillAsync("#username", _fixture.UserName);
        await _page.FillAsync("#password", _fixture.Password);
        await _page.EvaluateAsync("() => { var p = document.querySelector('#pwd'); if (p) p.value = document.querySelector('#password').value; }");
        await _page.ClickAsync("#login-btn");
        await _page.WaitForURLAsync(url => !url.Contains("/Admin/User/Login", StringComparison.OrdinalIgnoreCase), new PageWaitForURLOptions { Timeout = 30_000 });
    }

    public async Task DisposeAsync()
    {
        if (_context != null) await _context.DisposeAsync();
        if (_browser != null) await _browser.DisposeAsync();
        _playwright?.Dispose();
    }

    [Fact(DisplayName = "登录后落地看板中心，后台入口指向 /Admin")]
    public async Task Login_LandsOnDashboard()
    {
        Assert.EndsWith("/", _page!.Url);
        Assert.DoesNotContain("/Login", _page.Url);

        var admin = await _page.GetAttributeAsync("a[title='管理后台']", "href");
        Assert.Equal("/Admin", admin);

        // 管理后台首页仍是魔方原生工作台，不嵌入看板
        var resp = await _page.GotoAsync("/Admin", new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 30_000 });
        Assert.Equal(200, resp?.Status);
        Assert.Equal(0, await _page.Locator(".sd-rail").CountAsync());
    }

    [Fact(DisplayName = "管理后台可以打开看板中心并跳出后台框架")]
    public async Task AdminMenu_OpensDashboard()
    {
        await _page!.GotoAsync("/Admin", new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 30_000 });

        var link = _page.Locator("[data-url='/Dashboard']").First;
        Assert.Equal(1, await link.CountAsync());

        // 菜单默认折叠，直接派发 click 事件触发 layui 的打开逻辑
        await link.DispatchEventAsync("click");
        await _page.WaitForURLAsync(url => url.Contains("/Dashboard", StringComparison.OrdinalIgnoreCase), new PageWaitForURLOptions { Timeout = 30_000 });

        // 跳出后台内容框架：只剩看板自己的侧栏，不再套后台外壳
        Assert.Equal(1, await _page.Locator(".sd-rail").CountAsync());
        Assert.Equal(0, await _page.Locator(".layui-side").CountAsync());
    }

    [Fact(DisplayName = "看板菜单已注册并可进入系统级看板")]
    public async Task DashboardMenu_Registered()
    {
        var menus = _fixture.GetMenuPages();
        Assert.Contains(menus, e => String.Equals(e.Url, "/Dashboard", StringComparison.OrdinalIgnoreCase) || e.Url.StartsWith("/Dashboard/", StringComparison.OrdinalIgnoreCase));

        var resp = await _page!.GotoAsync("/Dashboard", new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 30_000 });
        Assert.Equal(200, resp?.Status);

        await _page.WaitForURLAsync(url => url.Contains("/Dashboard/System", StringComparison.OrdinalIgnoreCase), new PageWaitForURLOptions { Timeout = 30_000 });
        Assert.Contains("星尘看板", await _page.TitleAsync());
    }

    [Fact(DisplayName = "五个看板页面均可访问且无脚本异常")]
    public async Task DashboardPages_LoadWithoutErrors()
    {
        var errors = new List<String>();
        _page!.PageError += (_, e) => errors.Add(e);

        foreach (var (url, page) in _pages)
        {
            var resp = await _page.GotoAsync(url, new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 30_000 });
            Assert.Equal(200, resp?.Status);

            await _page.WaitForFunctionAsync("() => !!window.SD && !!window.SDCharts", null, new PageWaitForFunctionOptions { Timeout = 15_000 });

            var body = await _page.Locator("body").InnerTextAsync();
            Assert.DoesNotContain("Server Error", body);
            Assert.DoesNotContain("An unhandled exception", body);

            var actual = await _page.EvaluateAsync<String>("() => document.documentElement.dataset.page");
            Assert.Equal(page, actual);

            _output.WriteLine($"{url} -> {resp?.Status}，标题 {await _page.TitleAsync()}");
        }

        Assert.True(errors.Count == 0, "看板页面出现脚本异常：" + String.Join(" | ", errors));
    }

    [Fact(DisplayName = "看板数据接口返回 JSON 且包含范围信息")]
    public async Task DashboardData_ReturnsJson()
    {
        var result = await _page!.EvaluateAsync<DataProbe>(@"async () => {
            const r = await fetch('/Dashboard/Data?page=system&range=24h&id=0', { headers: { Accept: 'application/json' } });
            const body = await r.json();
            return { status: r.status, kpiCount: body.kpis ? body.kpis.length : 0, mode: body.scope ? body.scope.mode : '' };
        }");

        Assert.NotNull(result);
        Assert.Equal(200, result!.Status);
        Assert.Equal(6, result.KpiCount);
        Assert.False(String.IsNullOrEmpty(result.Mode));
    }

    /// <summary>数据接口探针结果</summary>
    public sealed class DataProbe
    {
        /// <summary>HTTP 状态码</summary>
        public Int32 Status { get; set; }

        /// <summary>KPI 数量</summary>
        public Int32 KpiCount { get; set; }

        /// <summary>可见范围模式</summary>
        public String? Mode { get; set; }
    }

    [Fact(DisplayName = "看板卡片均提供有效下钻链接")]
    public async Task Cards_HaveDrilldownLinks()
    {
        await _page!.GotoAsync("/Dashboard/System", new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 30_000 });
        await _page.WaitForFunctionAsync("() => !!window.SD", null, new PageWaitForFunctionOptions { Timeout = 15_000 });

        var cards = _page.Locator("[data-card]");
        var count = await cards.CountAsync();
        Assert.True(count >= 12, $"系统看板卡片数量异常：{count}");

        for (var i = 0; i < count; i++)
        {
            var card = cards.Nth(i);
            var id = await card.GetAttributeAsync("data-card");
            var anchors = card.Locator("a[href]");
            var link = await anchors.CountAsync() > 0 ? await anchors.First.GetAttributeAsync("href") : null;
            if (String.IsNullOrWhiteSpace(link)) link = await card.GetAttributeAsync("data-kpi-url");

            Assert.False(String.IsNullOrWhiteSpace(link), $"卡片 {id} 缺少下钻链接");
            Assert.StartsWith("/", link);
        }
    }

    [Fact(DisplayName = "布局调整可持久化并可恢复默认")]
    public async Task Layout_ChangesPersistAndReset()
    {
        await _page!.GotoAsync("/Dashboard/System", new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 30_000 });
        await _page.WaitForFunctionAsync("() => !!window.SD && !!window.SD.layout", null, new PageWaitForFunctionOptions { Timeout = 15_000 });
        await _page.EvaluateAsync(@"() => {
            Object.keys(localStorage)
                .filter(k => k.indexOf('sd-dashboard-layout:') === 0)
                .forEach(k => localStorage.removeItem(k));
        }");
        await _page.ReloadAsync(new PageReloadOptions { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 30_000 });

        var card = _page.Locator("[data-card=\"system-top-apps\"]");
        Assert.True(await card.IsVisibleAsync());

        await _page.ClickAsync("[data-action=\"layout\"]");
        var row = _page.Locator("[data-layout-id=\"system-top-apps\"]");
        await row.Locator("[data-layout-size]").SelectOptionAsync("24");
        await row.Locator("[data-layout-visible]").UncheckAsync();
        await _page.ClickAsync(".sd-layout__foot [data-layout-close]");

        await _page.ReloadAsync(new PageReloadOptions { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 30_000 });
        await _page.WaitForFunctionAsync("() => !!window.SD && !!window.SD.layout", null, new PageWaitForFunctionOptions { Timeout = 15_000 });
        Assert.False(await card.IsVisibleAsync());
        Assert.Equal("24", await card.GetAttributeAsync("data-size"));

        await _page.ClickAsync("[data-action=\"layout\"]");
        await _page.ClickAsync("[data-layout-reset]");
        await _page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
        await _page.WaitForFunctionAsync("() => !!window.SD && !!window.SD.layout", null, new PageWaitForFunctionOptions { Timeout = 15_000 });
        Assert.True(await card.IsVisibleAsync());
    }

    [Fact(DisplayName = "大屏视角设置可持久化")]
    public async Task WallboardSettings_Persist()
    {
        await _page!.GotoAsync("/Dashboard/Wallboard", new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 30_000 });
        await _page.WaitForFunctionAsync("() => !!window.SD", null, new PageWaitForFunctionOptions { Timeout = 15_000 });
        await _page.EvaluateAsync("() => localStorage.removeItem('sd-wallboard-settings:v1')");
        await _page.ReloadAsync(new PageReloadOptions { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 30_000 });

        await _page.ClickAsync("[data-action=\"wallboard-settings\"]");
        var panel = _page.Locator("#wbSettings");
        await panel.WaitForAsync(new LocatorWaitForOptions { Timeout = 10_000 });
        await panel.Locator("[data-wb-view=\"1\"]").UncheckAsync();
        await panel.Locator("[data-wb-interval]").SelectOptionAsync("30000");
        await panel.Locator(".wb-settings__foot [data-wb-close]").ClickAsync();

        await _page.ReloadAsync(new PageReloadOptions { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 30_000 });
        await _page.WaitForFunctionAsync("() => !!window.SD", null, new PageWaitForFunctionOptions { Timeout = 15_000 });

        Assert.False(await _page.Locator("#wbDots button[data-view=\"1\"]").IsVisibleAsync());

        await _page.ClickAsync("[data-action=\"wallboard-settings\"]");
        panel = _page.Locator("#wbSettings");
        await panel.WaitForAsync(new LocatorWaitForOptions { Timeout = 10_000 });
        Assert.False(await panel.Locator("[data-wb-view=\"1\"]").IsCheckedAsync());
        Assert.Equal("30000", await panel.Locator("[data-wb-interval]").InputValueAsync());

        await panel.Locator(".wb-settings__foot [data-wb-close]").ClickAsync();
        await _page.EvaluateAsync("() => localStorage.removeItem('sd-wallboard-settings:v1')");
    }

    [Fact(DisplayName = "主题三态切换生效且不刷新页面")]
    public async Task ThemeSwitch_AppliesWithoutReload()
    {
        await _page!.GotoAsync("/Dashboard/System", new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 30_000 });
        await _page.WaitForFunctionAsync("() => !!window.SD", null, new PageWaitForFunctionOptions { Timeout = 15_000 });

        await _page.EvaluateAsync("() => { window.__themeMarker = 1; }");

        var buttons = _page.Locator("[data-theme-switch] button[data-theme-mode]");
        var count = await buttons.CountAsync();
        Assert.True(count >= 3, $"主题切换应有三种模式，实际 {count} 个按钮");

        await buttons.Nth(2).ClickAsync();  // 深色
        await _page.WaitForTimeoutAsync(500);
        var dark = await _page.EvaluateAsync<String>("() => document.documentElement.dataset.theme");
        Assert.Equal("dark", dark);
        var marker = await _page.EvaluateAsync<Double>("() => window.__themeMarker || 0");
        Assert.Equal(1d, marker);

        await buttons.Nth(1).ClickAsync();  // 浅色
        await _page.WaitForTimeoutAsync(500);
        var light = await _page.EvaluateAsync<String>("() => document.documentElement.dataset.theme");
        Assert.Equal("light", light);
    }
}
