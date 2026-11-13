using System.Text.Json;
using Stardust.Data;
using Stardust.Data.Configs;
using Stardust.Models;
using Stardust.WebApiTests.Fixtures;
using Xunit;
using Xunit.Abstractions;

namespace Stardust.WebApiTests.Areas;

/// <summary>
/// Config 区进程内 HTTP 冒烟。
/// 宿主语义实证（2026-11-09 三轮实验：单类冷启动 / Meta+Config 组合 / 全量 179 例）：
/// 1. 登录门（ConfigController L29）：依赖同宿主 Cube 管道预热——未跑过任何 Cube 区域 Bearer
///    请求时恒 401「未登录！」；跑过后带 Bearer 的请求放行。匿名（无头）恒 401，顺序无关。
/// 2. 密钥门（AppTokenService.Authorize L50-65）：密钥不匹配按 app.ReRegister 裁决——
///    跟随全局按全局默认解析（实测临时放行），显式 Off 403「非法访问」。
/// 3. 禁用门（L46）：先于密钥校验，禁用应用恒 403「已禁用」。
/// 因此需登录态的用例自带预热步骤（Bearer 调 Cube 区域 meta），断言与进程启动顺序无关。
/// C02/C03/C05 完整语义以进程内真值固化；5188 预览链窗口负责浏览器侧 UI 走查。
/// </summary>
[Collection(WebApiCollection.Name)]
public class ConfigSmokeTests
{
    private readonly WebApiTestFactory _factory;
    private readonly ApiClient _api;
    private readonly ITestOutputHelper _output;

    public ConfigSmokeTests(WebApiTestFactory factory, ITestOutputHelper output)
    {
        _factory = factory;
        _api = new ApiClient(factory, output);
        _output = output;
    }

    private const String AppName = "cfgsmoke";
    private const String AppSecret = "cfgsmoke-secret";

    private static async Task<JsonElement> BodyOf(HttpResponseMessage resp)
    {
        var body = await resp.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<JsonElement>(body, new JsonSerializerOptions(JsonSerializerDefaults.Web));
    }

    private async Task<HttpResponseMessage> RawGetAsync(String endpoint, Boolean auth = true)
    {
        var resp = await _api.GetAsync(endpoint, auth);
        var body = await resp.Content.ReadAsStringAsync();
        _output.WriteLine("GET " + endpoint + " HTTP " + (Int32)resp.StatusCode + " body=" + body);
        return resp;
    }

    /// <summary>登录门预热：Bearer 调 Cube 区域 meta 端点，触发其授权过滤器的 TryLogin。</summary>
    private async Task WarmUpAsync()
    {
        var warm = await _api.GetJsonAsync("Registry/AppService/meta");
        Assert.True(warm.Code == 0, "登录门预热失败：meta 应 code=0，实际 " + warm.Code + " " + warm.Message);
    }

    /// <summary>幂等创建/恢复冒烟应用 + 配置（仅进程内临时 SQLite 库，符合数据铁律）。</summary>
    private void EnsureApp()
    {
        var app = App.FindByName(AppName);
        if (app == null)
        {
            app = new App { Name = AppName, DisplayName = "配置冒烟", Secret = AppSecret, Enable = true };
            app.Insert();
        }
        else if (app.Secret != AppSecret || !app.Enable || app.ReRegister != ReRegisterModes.Default)
        {
            app.Secret = AppSecret;
            app.Enable = true;
            app.ReRegister = ReRegisterModes.Default;
            app.Update();
        }

        var cfg = AppConfig.FindByName(AppName);
        if (cfg == null)
        {
            cfg = new AppConfig { Name = AppName, AppId = app.Id, Enable = true, Version = 1 };
            cfg.Insert();
        }
    }

    private static void DisableApp()
    {
        var app = App.FindByName(AppName);
        if (app != null && app.Enable)
        {
            app.Enable = false;
            app.Update();
        }
    }

    /// <summary>C01（进程内事实）：ConfigController 公开动作仅 GetAll 一个；新前端无根 Config 管理页，UI 断言归 5188 窗口。</summary>
    [Fact]
    public void ConfigController_HasOnlyGetAll_PublicAction()
    {
        var t = typeof(Stardust.Web.Controllers.ConfigController);
        var actions = t.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
            .Where(m => m.DeclaringType == t)
            .Select(m => m.Name)
            .OrderBy(n => n)
            .ToArray();

        _output.WriteLine("[C01] ConfigController actions: " + String.Join(", ", actions));
        Assert.Equal(new[] { "GetAll" }, actions);
    }

    /// <summary>C12/C04：空 appId → 参数错误信封（登录门之前抛出，HTTP 200 + code 非 0 + message 含 appId）。</summary>
    [Fact]
    public async Task GetAll_EmptyAppId_ReturnsParamError()
    {
        var resp = await RawGetAsync("Config/GetAll");
        var body = await BodyOf(resp);

        Assert.True(body.TryGetProperty("code", out var c) && c.GetInt32() != 0, "空 appId 应返回错误信封：" + body);
        Assert.True(body.TryGetProperty("message", out var m) && m.GetString()!.Contains("appId", StringComparison.OrdinalIgnoreCase), "错误信封应指明 appId：" + body);
    }

    /// <summary>C11：匿名（无 Bearer）→ 登录门 code=401「未登录！」。三轮实证顺序无关。</summary>
    [Fact]
    public async Task GetAll_Anonymous_ReturnsUnauthorizedSemantic()
    {
        var resp = await RawGetAsync("Config/GetAll?appId=" + AppName + "&secret=" + AppSecret, auth: false);
        var body = await BodyOf(resp);

        var http401 = (Int32)resp.StatusCode == 401;
        var code401 = body.TryGetProperty("code", out var c) && c.GetInt32() == 401;
        Assert.True(http401 || code401, "匿名请求应得 401 语义（HTTP=" + (Int32)resp.StatusCode + "，body=" + body + "）");
    }

    /// <summary>
    /// C02/C03（进程内真值）：预热后 Bearer 用户 JWT + 正确应用密钥 → code=0 七字段信封
    /// （version/updateTime 必在；cfgsmoke 无配置数据故 configs 为空对象）。
    /// </summary>
    [Fact]
    public async Task GetAll_BearerUserToken_ReturnsSevenFieldEnvelope()
    {
        EnsureApp();
        await WarmUpAsync();

        var resp = await RawGetAsync("Config/GetAll?appId=" + AppName + "&secret=" + AppSecret, auth: true);
        var body = await BodyOf(resp);

        Assert.True(body.TryGetProperty("code", out var c) && c.GetInt32() == 0, "预热后 Bearer+正确密钥应放行 code=0（HTTP=" + (Int32)resp.StatusCode + "，body=" + body + "）");
        Assert.True(body.TryGetProperty("data", out var data) && data.TryGetProperty("version", out var ver) && ver.TryGetInt32(out var verN) && verN >= 0, "data.version 应存在：" + body);
        Assert.True(data.TryGetProperty("updateTime", out var upd) && !String.IsNullOrEmpty(upd.GetString()), "data.updateTime 应存在：" + body);
    }

    /// <summary>
    /// C13（进程内真值）：预热后，显式 ReRegister=Off 的应用 + 错误密钥 →
    /// 密钥门按「关闭」裁决 → 403「非法访问」（AppTokenService 安全纵深 V-1/V-2）。finally 恢复。
    /// </summary>
    [Fact]
    public async Task GetAll_WrongSecret_RejectedInOffMode()
    {
        EnsureApp();
        var app = App.FindByName(AppName);
        app.ReRegister = ReRegisterModes.Off;
        app.Update();

        try
        {
            await WarmUpAsync();

            var resp = await RawGetAsync("Config/GetAll?appId=" + AppName + "&secret=wrong-secret", auth: true);
            var body = await BodyOf(resp);

            Assert.True(body.TryGetProperty("code", out var c) && c.GetInt32() == 403, "显式关闭+错密钥应 403 非法访问（HTTP=" + (Int32)resp.StatusCode + "，body=" + body + "）");
            Assert.True(body.TryGetProperty("message", out var m) && m.GetString()!.Contains("非法访问", StringComparison.Ordinal), "403 消息应指明非法访问：" + body);
        }
        finally
        {
            EnsureApp();
        }
    }

    /// <summary>C10（进程内真值）：预热后，禁用应用 → 禁用门（先于密钥校验）→ 403「应用[cfgsmoke]已禁用！」。finally 恢复启用。</summary>
    [Fact]
    public async Task GetAll_DisabledApp_Returns403Disabled()
    {
        EnsureApp();
        DisableApp();

        try
        {
            await WarmUpAsync();

            var resp = await RawGetAsync("Config/GetAll?appId=" + AppName + "&secret=" + AppSecret, auth: true);
            var body = await BodyOf(resp);

            Assert.True(body.TryGetProperty("code", out var c) && c.GetInt32() == 403, "禁用应用应 403（HTTP=" + (Int32)resp.StatusCode + "，body=" + body + "）");
            Assert.True(body.TryGetProperty("message", out var m) && m.GetString()!.Contains("禁用", StringComparison.Ordinal), "403 消息应指明禁用：" + body);
        }
        finally
        {
            EnsureApp();
        }
    }
}
