using System.Text.Json;
using Stardust.WebApiTests.Fixtures;
using Xunit;
using Xunit.Abstractions;

namespace Stardust.WebApiTests.Areas;

/// <summary>
/// AgentController 路由别名集成测试（lead 裁定 A 双路由，2026-11-10 P2）。
/// </summary>
/// <remarks>
/// 背景：WebApi 根目录 AgentController 仅有裸路由 /Agent、/Agent/Probe，不符 WebApi api/[controller] 惯例，
/// Vue 请求层按 routeToApiPrefix 拼 /api 后 404（j-node-b task-51 走查抓出，修前证据帧 F-NODE-AGENT-ROUTE）。
/// 前置兼容性 grep：仓内硬编码裸 6681 消费者=0；SVR-安装引导页.md:125「/Agent?format=json 保持旧接口不变，
/// 兼容存量客户端」为仓外契约面 → lead 19:2x 裁定 A：双路由并存（裸旧路径保留 + /api 惯例别名新增），
/// ConfigController 不动（configCenter 前端修已闭其面）。
/// 断言语义：双路径各返回 200 + code=0 信封；探活端点不校验 online 值（依赖本机 Agent 环境）。
/// </remarks>
[Collection(WebApiCollection.Name)]
public class AgentRouteTests
{
    private readonly WebApiTestFactory _factory;
    private readonly ApiClient _api;
    private readonly ITestOutputHelper _output;

    public AgentRouteTests(WebApiTestFactory factory, ITestOutputHelper output)
    {
        _factory = factory;
        _api = new ApiClient(factory, output);
        _output = output;
    }

    private static async Task<JsonElement> BodyOf(HttpResponseMessage resp)
    {
        var body = await resp.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<JsonElement>(body, new JsonSerializerOptions(JsonSerializerDefaults.Web));
    }

    private async Task<HttpResponseMessage> AnonymousGetAsync(String path)
    {
        var resp = await _api.GetAsync(path, auth: false);
        var body = await resp.Content.ReadAsStringAsync();
        _output.WriteLine("GET " + path + " HTTP " + (Int32)resp.StatusCode + " body=" + body);
        return resp;
    }

    /// <summary>裸 /Agent 旧接口承诺保留：下载目录 JSON，200 + code=0 信封。</summary>
    [Fact]
    public async Task Index_BareRoute_Returns200Envelope()
    {
        var resp = await AnonymousGetAsync("/Agent");

        Assert.True((Int32)resp.StatusCode == 200, "裸 /Agent 旧接口应 200（SVR-安装引导页.md:125 承诺），实际 " + (Int32)resp.StatusCode);
        var body = await BodyOf(resp);
        Assert.True(body.TryGetProperty("code", out var c) && c.GetInt32() == 0, "应返回 code=0 信封：" + body);
        Assert.True(body.TryGetProperty("data", out var data) && data.TryGetProperty("agent", out _), "data.agent 下载清单应存在：" + body);
    }

    /// <summary>/api/Agent 惯例别名（裁定 A 新增）：与裸路由同语义，200 + code=0 信封。</summary>
    [Fact]
    public async Task Index_ApiPrefixRoute_Returns200Envelope()
    {
        var resp = await AnonymousGetAsync("/api/Agent");

        Assert.True((Int32)resp.StatusCode == 200, "/api/Agent 惯例别名应 200（Vue 请求层按惯例拼 /api），实际 " + (Int32)resp.StatusCode);
        var body = await BodyOf(resp);
        Assert.True(body.TryGetProperty("code", out var c) && c.GetInt32() == 0, "应返回 code=0 信封：" + body);
        Assert.True(body.TryGetProperty("data", out var data) && data.TryGetProperty("agent", out _), "data.agent 下载清单应存在：" + body);
    }

    /// <summary>裸 /Agent/Probe 旧接口承诺保留：本机探活，200 + code=0（online 值依赖本机 Agent，不校验）。</summary>
    [Fact]
    public async Task Probe_BareRoute_Returns200Envelope()
    {
        var resp = await AnonymousGetAsync("/Agent/Probe");

        Assert.True((Int32)resp.StatusCode == 200, "裸 /Agent/Probe 旧接口应 200，实际 " + (Int32)resp.StatusCode);
        var body = await BodyOf(resp);
        Assert.True(body.TryGetProperty("code", out var c) && c.GetInt32() == 0, "应返回 code=0 信封：" + body);
        Assert.True(body.TryGetProperty("data", out var data) && data.TryGetProperty("online", out _), "data.online 应存在：" + body);
    }

    /// <summary>/api/Agent/Probe 惯例别名（裁定 A 新增）：与裸路由同语义。</summary>
    [Fact]
    public async Task Probe_ApiPrefixRoute_Returns200Envelope()
    {
        var resp = await AnonymousGetAsync("/api/Agent/Probe");

        Assert.True((Int32)resp.StatusCode == 200, "/api/Agent/Probe 惯例别名应 200，实际 " + (Int32)resp.StatusCode);
        var body = await BodyOf(resp);
        Assert.True(body.TryGetProperty("code", out var c) && c.GetInt32() == 0, "应返回 code=0 信封：" + body);
        Assert.True(body.TryGetProperty("data", out var data) && data.TryGetProperty("online", out _), "data.online 应存在：" + body);
    }
}
