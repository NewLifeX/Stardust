using System.Net.Http.Json;
using System.Text.Json;
using NewLife.Cube;
using Xunit;
using Xunit.Abstractions;

namespace Stardust.WebApiTests.Fixtures;

/// <summary>
/// 集成测试的 API 客户端：负责请求发送、URL 规范化与统一信封校验，
/// 把每个请求的 URL / 状态码 / 响应体写进 xUnit 测试输出，断言失败时可快速定位。
/// </summary>
/// <remarks>
/// 只做「信封级」断言（HTTP 成功 + 可反序列化 + Code==0），业务断言留给各个测试方法，
/// 避免把「断言什么」藏进基类导致失败信息只剩一句 InvalidOperationException。
/// </remarks>
public sealed class ApiClient
{
    /// <summary>与 HttpClient 的 ReadFromJsonAsync 默认行为一致（Web 约定：小驼峰 + 大小写不敏感）。</summary>
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    private readonly WebApiTestFactory _factory;
    private readonly ITestOutputHelper _output;

    public ApiClient(WebApiTestFactory factory, ITestOutputHelper output)
    {
        _factory = factory;
        _output = output;
    }

    #region URL 规范化
    /// <summary>
    /// 区域控制器走 Cube 的区域路由模板 <c>api/{area}/{controller}/{action}</c>，需要 /api 前缀；
    /// 少数控制器用特性路由自成一派，这里显式登记，新增特性路由控制器时只需加一行。
    /// </summary>
    private static readonly (String EndpointPrefix, String UrlPrefix)[] _routeRules =
    [
        // [Route("[controller]/[action]")]
        ("Auth/", "/"),
        // [Route("[area]/[controller]/[action]")]
        ("Deployment/PipelineWebhook/", "/"),
        // Lov 控制器位于 Admin 区域，测试中简写为 Lov/...
        ("Lov/", "/api/Admin/"),
    ];

    /// <summary>把测试里书写的端点规范化为可请求的绝对 URL。</summary>
    public static String NormalizeUrl(String endpoint)
    {
        var e = endpoint.TrimStart('/');
        if (e.StartsWith("api/", StringComparison.OrdinalIgnoreCase)) return "/" + e;

        foreach (var (endpointPrefix, urlPrefix) in _routeRules)
        {
            if (e.StartsWith(endpointPrefix, StringComparison.OrdinalIgnoreCase)) return urlPrefix + e;
        }

        return "/api/" + e;
    }
    #endregion

    /// <summary>GET 请求。auth=true 用带 Bearer 的认证客户端，否则匿名客户端。</summary>
    public async Task<HttpResponseMessage> GetAsync(String endpoint, Boolean auth = true) =>
        await SendAsync(client => client.GetAsync(NormalizeUrl(endpoint)), "GET", endpoint, auth);

    /// <summary>POST JSON 请求。auth 同上。</summary>
    public async Task<HttpResponseMessage> PostAsJsonAsync<T>(String endpoint, T payload, Boolean auth = true) =>
        await SendAsync(client => client.PostAsJsonAsync(NormalizeUrl(endpoint), payload, _json), "POST", endpoint, auth);

    /// <summary>GET 并校验统一信封，返回强类型响应供测试做业务断言。</summary>
    public async Task<ApiResponse<T>> GetApiAsync<T>(String endpoint, Boolean auth = true)
    {
        var resp = await GetAsync(endpoint, auth);
        return await ToApiResponseAsync<T>(resp, endpoint);
    }

    /// <summary>POST JSON 并校验统一信封，返回强类型响应供测试做业务断言。</summary>
    public async Task<ApiResponse<T>> PostApiAsync<T>(String endpoint, Object? payload, Boolean auth = true)
    {
        var resp = await PostAsJsonAsync(endpoint, payload, auth);
        return await ToApiResponseAsync<T>(resp, endpoint);
    }

    /// <summary>data 结构不固定的端点（页面、字段列表等）：用 JsonElement 承载。</summary>
    public Task<ApiResponse<JsonElement>> GetJsonAsync(String endpoint, Boolean auth = true) =>
        GetApiAsync<JsonElement>(endpoint, auth);

    private async Task<HttpResponseMessage> SendAsync(Func<HttpClient, Task<HttpResponseMessage>> send, String method, String endpoint, Boolean auth)
    {
        var url = NormalizeUrl(endpoint);
        var client = auth ? await _factory.CreateAuthenticatedClient() : _factory.CreateClient();

        _output.WriteLine($"→ {method} {url} (auth={auth})");
        var resp = await send(client);
        _output.WriteLine($"← {method} {url} -> {(Int32)resp.StatusCode} {resp.StatusCode}");

        return resp;
    }

    /// <summary>校验统一信封：HTTP 成功 + 可反序列化为 ApiResponse&lt;T&gt; + Code==0。</summary>
    private async Task<ApiResponse<T>> ToApiResponseAsync<T>(HttpResponseMessage resp, String endpoint)
    {
        var url = NormalizeUrl(endpoint);
        var raw = await resp.Content.ReadAsStringAsync();

        Assert.True(resp.IsSuccessStatusCode, $"[{url}] HTTP 状态码 {(Int32)resp.StatusCode} 非成功：{raw}");

        ApiResponse<T>? api = null;
        try
        {
            api = JsonSerializer.Deserialize<ApiResponse<T>>(raw, _json);
        }
        catch (JsonException ex)
        {
            Assert.Fail($"[{url}] 响应格式不符合 ApiResponse<{typeof(T).Name}>：{ex.Message}{Environment.NewLine}{raw}");
        }

        Assert.NotNull(api);
        Assert.Equal(0, api!.Code);

        return api;
    }
}
