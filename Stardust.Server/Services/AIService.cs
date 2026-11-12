using System.Diagnostics;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using NewLife;
using NewLife.Log;
using NewLife.Remoting;
using Stardust.Models;

namespace Stardust.Server.Services;

/// <summary>AI对话服务。把下游应用的对话请求转发到 OpenAI 兼容的上游服务</summary>
/// <remarks>
/// 服务端只保存一份上游密钥，下游应用使用星尘登录令牌调用，无需各自申请大模型密钥。
/// 支持星语网关、DeepSeek、OpenAI 等遵守 /v1/chat/completions 协议的服务，提供非流式与 SSE 流式两种输出。
/// </remarks>
public class AIService
{
    private static readonly JsonSerializerOptions _jsonOptions = new() { Encoder = JavaScriptEncoder.Create(UnicodeRanges.All) };

    private readonly IHttpClientFactory _httpClientFactory;

    /// <summary>实例化</summary>
    /// <param name="httpClientFactory">HTTP客户端工厂</param>
    public AIService(IHttpClientFactory httpClientFactory) => _httpClientFactory = httpClientFactory;

    /// <summary>对话。转发完整消息列表到上游，一次性返回完整回复</summary>
    /// <param name="model">对话请求</param>
    /// <param name="appId">调用方应用标识，作为上游 user 字段并写入埋点</param>
    /// <param name="ip">调用方地址，仅用于埋点</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>对话响应</returns>
    /// <exception cref="ApiException">未启用、未配置、参数非法或上游调用失败时抛出</exception>
    public async Task<AIChatResponse> ChatAsync(AIChatRequest model, String? appId, String? ip, CancellationToken cancellationToken)
    {
        var context = Prepare(model);
        var payload = BuildPayload(context, appId, false);

        using var span = DefaultTracer.Instance?.NewSpan("AI/Chat", new { appId, ip, model = context.Model, messages = context.Messages.Count });
        var sw = Stopwatch.StartNew();

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(context.Timeout));

        var http = CreateHttp(context);
        using var request = CreateRequest(context, payload);

        String text;
        try
        {
            using var response = await http.SendAsync(request, cts.Token).ConfigureAwait(false);
            text = await response.Content.ReadAsStringAsync(cts.Token).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                span?.AppendTag($"http={(Int32)response.StatusCode}");
                throw new ApiException(ApiCode.InternalServerError, L.T("AI upstream request failed: {0}", $"HTTP {(Int32)response.StatusCode}: {GetError(text)}"));
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            span?.AppendTag("timeout");
            throw new ApiException(ApiCode.InternalServerError, L.T("AI upstream request failed: {0}", $"timeout after {context.Timeout} seconds"));
        }
        catch (HttpRequestException ex)
        {
            span?.SetError(ex, null);
            throw new ApiException(ApiCode.InternalServerError, L.T("AI upstream request failed: {0}", ex.Message));
        }

        sw.Stop();
        span?.AppendTag($"elapsed={sw.ElapsedMilliseconds}ms");

        return ParseResponse(text, sw.ElapsedMilliseconds);
    }

    /// <summary>流式对话。按上游 SSE 逐段返回增量文本</summary>
    /// <param name="model">对话请求</param>
    /// <param name="appId">调用方应用标识，作为上游 user 字段并写入埋点</param>
    /// <param name="ip">调用方地址，仅用于埋点</param>
    /// <param name="cancellationToken">取消令牌。客户端断开时中止上游请求</param>
    /// <returns>增量序列。结束段带 <see cref="AIChatDelta.FinishReason"/></returns>
    /// <exception cref="ApiException">未启用、未配置、参数非法或上游调用失败时抛出</exception>
    public async IAsyncEnumerable<AIChatDelta> StreamAsync(AIChatRequest model, String? appId, String? ip, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var context = Prepare(model);
        var payload = BuildPayload(context, appId, true);

        using var span = DefaultTracer.Instance?.NewSpan("AI/ChatStream", new { appId, ip, model = context.Model, messages = context.Messages.Count });
        var sw = Stopwatch.StartNew();

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(context.Timeout));

        var http = CreateHttp(context);
        using var request = CreateRequest(context, payload);

        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            span?.AppendTag("timeout");
            throw new ApiException(ApiCode.InternalServerError, L.T("AI upstream request failed: {0}", $"timeout after {context.Timeout} seconds"));
        }
        catch (HttpRequestException ex)
        {
            span?.SetError(ex, null);
            throw new ApiException(ApiCode.InternalServerError, L.T("AI upstream request failed: {0}", ex.Message));
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                var text = await response.Content.ReadAsStringAsync(cts.Token).ConfigureAwait(false);
                span?.AppendTag($"http={(Int32)response.StatusCode}");
                throw new ApiException(ApiCode.InternalServerError, L.T("AI upstream request failed: {0}", $"HTTP {(Int32)response.StatusCode}: {GetError(text)}"));
            }

            using var stream = await response.Content.ReadAsStreamAsync(cts.Token).ConfigureAwait(false);
            using var reader = new StreamReader(stream, Encoding.UTF8);

            while (true)
            {
                var line = await reader.ReadLineAsync(cts.Token).ConfigureAwait(false);
                if (line == null) break;
                if (line.IsNullOrEmpty() || line[0] == ':') continue;
                if (!line.StartsWith("data:")) continue;

                var data = line[5..].Trim();
                if (data == "[DONE]") break;

                var delta = ParseDelta(data, sw.ElapsedMilliseconds);
                if (delta != null) yield return delta;
            }
        }

        span?.AppendTag($"elapsed={sw.ElapsedMilliseconds}ms");
    }

    /// <summary>解析 OpenAI 兼容的对话响应。上游 200 但返回 error 时同样按失败处理</summary>
    /// <param name="body">响应文本</param>
    /// <param name="elapsed">服务端耗时，单位毫秒</param>
    /// <returns>对话响应</returns>
    public static AIChatResponse ParseResponse(String body, Int64 elapsed)
    {
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(body);
        }
        catch (JsonException)
        {
            throw new ApiException(ApiCode.InternalServerError, L.T("AI upstream returned an invalid response: {0}", GetError(body)));
        }

        using (doc)
        {
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                throw new ApiException(ApiCode.InternalServerError, L.T("AI upstream returned an invalid response: {0}", GetError(body)));

            if (root.TryGetProperty("error", out _))
                throw new ApiException(ApiCode.InternalServerError, L.T("AI upstream request failed: {0}", GetError(body)));

            if (!root.TryGetProperty("choices", out var choices) || choices.ValueKind != JsonValueKind.Array || choices.GetArrayLength() == 0)
                throw new ApiException(ApiCode.InternalServerError, L.T("AI upstream returned an invalid response: {0}", GetError(body)));

            var choice = choices[0];
            var content = choice.TryGetProperty("message", out var message) && message.TryGetProperty("content", out var value)
                ? value.GetString()
                : null;

            var rs = new AIChatResponse
            {
                Content = content,
                Model = root.TryGetProperty("model", out var model) ? model.GetString() : null,
                FinishReason = choice.TryGetProperty("finish_reason", out var finish) ? finish.GetString() : null,
                Elapsed = elapsed,
            };

            if (root.TryGetProperty("usage", out var usage) && usage.ValueKind == JsonValueKind.Object)
                rs.Usage = ParseUsage(usage);

            return rs;
        }
    }

    /// <summary>解析对话接口地址。支持填写基础地址、以 /v1 结尾的地址或完整地址</summary>
    /// <param name="url">配置的地址</param>
    /// <returns>完整的 chat/completions 地址；配置为空时返回空字符串</returns>
    public static String ResolveUrl(String? url)
    {
        if (url.IsNullOrWhiteSpace()) return "";

        url = url.Trim().TrimEnd('/');
        if (url.EndsWithIgnoreCase("/chat/completions")) return url;
        if (url.EndsWithIgnoreCase("/v1")) return url + "/chat/completions";

        return url + "/v1/chat/completions";
    }

    #region 辅助
    private sealed class ChatContext
    {
        public String Url { get; init; } = null!;
        public String Model { get; init; } = null!;
        public List<AIChatMessage> Messages { get; init; } = null!;
        public Double Temperature { get; init; }
        public Int32 MaxTokens { get; init; }
        public Int32 Timeout { get; init; }
        public String? Key { get; init; }
    }

    /// <summary>校验开关与配置，组装上游请求上下文</summary>
    /// <param name="model">对话请求</param>
    /// <returns>请求上下文</returns>
    /// <exception cref="ApiException">未启用、未配置或参数非法时抛出</exception>
    private static ChatContext Prepare(AIChatRequest model)
    {
        if (model == null) throw new ApiException(ApiCode.BadRequest, L.T("AI request contains no valid message."));

        var set = StarServerSetting.Current;
        if (!set.EnableAI) throw new ApiException(ApiCode.Forbidden, L.T("AI chat is disabled."));

        var url = ResolveUrl(set.AIUrl);
        if (url.IsNullOrEmpty() || set.AIModel.IsNullOrWhiteSpace())
            throw new ApiException(ApiCode.InternalServerError, L.T("The AI service is not configured. Set AIUrl and AIModel in the platform settings."));

        // 校验消息并组装上下文。角色统一小写，空内容直接拒绝，避免把脏数据转给上游
        var messages = new List<AIChatMessage>();
        foreach (var item in model.Messages ?? [])
        {
            if (item == null || item.Content.IsNullOrWhiteSpace())
                throw new ApiException(ApiCode.BadRequest, L.T("AI request contains no valid message."));

            messages.Add(new AIChatMessage
            {
                Role = item.Role.IsNullOrWhiteSpace() ? "user" : item.Role.Trim().ToLowerInvariant(),
                Content = item.Content,
            });
        }
        if (messages.Count == 0) throw new ApiException(ApiCode.BadRequest, L.T("AI request contains no valid message."));

        // 请求自带系统提示词优先，其次平台默认提示词；消息列表中已有 system 消息时不重复插入
        if (!messages.Any(e => e.Role.EqualIgnoreCase("system")))
        {
            var system = !model.SystemPrompt.IsNullOrWhiteSpace() ? model.SystemPrompt : set.AISystemPrompt;
            if (!system.IsNullOrWhiteSpace()) messages.Insert(0, new AIChatMessage { Role = "system", Content = system });
        }

        // 请求指定模型时必须落在允许清单内，防止下游误用高成本模型
        var code = model.Model.IsNullOrWhiteSpace() ? set.AIModel : model.Model.Trim();
        var allowed = (set.AIModels ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        if (!set.AIModel.IsNullOrWhiteSpace() && !allowed.Any(e => e.EqualIgnoreCase(set.AIModel))) allowed.Add(set.AIModel);
        if (allowed.Count > 0 && !allowed.Any(e => e.EqualIgnoreCase(code)))
            throw new ApiException(ApiCode.BadRequest, L.T("AI model [{0}] is not allowed.", code));

        return new ChatContext
        {
            Url = url,
            Model = code,
            Messages = messages,
            Temperature = model.Temperature ?? set.AITemperature,
            MaxTokens = model.MaxTokens is > 0 ? model.MaxTokens.Value : set.AIMaxTokens,
            Timeout = set.AITimeout > 0 ? set.AITimeout : 120,
            Key = set.AIKey,
        };
    }

    /// <summary>组装 OpenAI 兼容请求体</summary>
    /// <param name="context">请求上下文</param>
    /// <param name="appId">调用方应用标识</param>
    /// <param name="stream">是否流式</param>
    /// <returns>可序列化的请求体</returns>
    private static Dictionary<String, Object?> BuildPayload(ChatContext context, String? appId, Boolean stream)
    {
        var payload = new Dictionary<String, Object?>
        {
            ["model"] = context.Model,
            ["messages"] = context.Messages.Select(e => new Dictionary<String, Object?>
            {
                ["role"] = e.Role,
                ["content"] = e.Content,
            }).ToList(),
            ["stream"] = stream,
        };
        if (context.Temperature > 0) payload["temperature"] = context.Temperature;
        if (context.MaxTokens > 0) payload["max_tokens"] = context.MaxTokens;
        if (!appId.IsNullOrEmpty()) payload["user"] = appId;

        return payload;
    }

    private HttpClient CreateHttp(ChatContext context)
    {
        var http = _httpClientFactory.CreateClient(nameof(AIService));
        http.Timeout = TimeSpan.FromSeconds(context.Timeout);

        return http;
    }

    private static HttpRequestMessage CreateRequest(ChatContext context, Dictionary<String, Object?> payload)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, context.Url)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload, _jsonOptions), Encoding.UTF8, "application/json"),
        };
        if (!context.Key.IsNullOrEmpty()) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", context.Key.Trim());

        return request;
    }

    /// <summary>解析流式响应中的单个 data 段</summary>
    /// <param name="body">data 段 JSON</param>
    /// <param name="elapsed">服务端耗时，单位毫秒</param>
    /// <returns>增量对象；空增量返回 null</returns>
    private static AIChatDelta? ParseDelta(String body, Int64 elapsed)
    {
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(body);
        }
        catch (JsonException)
        {
            throw new ApiException(ApiCode.InternalServerError, L.T("AI upstream returned an invalid response: {0}", GetError(body)));
        }

        using (doc)
        {
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                throw new ApiException(ApiCode.InternalServerError, L.T("AI upstream returned an invalid response: {0}", GetError(body)));

            if (root.TryGetProperty("error", out _))
                throw new ApiException(ApiCode.InternalServerError, L.T("AI upstream request failed: {0}", GetError(body)));

            var delta = new AIChatDelta
            {
                Model = root.TryGetProperty("model", out var model) ? model.GetString() : null,
            };
            if (root.TryGetProperty("choices", out var choices) && choices.ValueKind == JsonValueKind.Array && choices.GetArrayLength() > 0)
            {
                var choice = choices[0];
                if (choice.TryGetProperty("delta", out var deltaNode) && deltaNode.TryGetProperty("content", out var content))
                    delta.Content = content.GetString();
                if (choice.TryGetProperty("finish_reason", out var finish))
                    delta.FinishReason = finish.GetString();
            }
            if (root.TryGetProperty("usage", out var usage) && usage.ValueKind == JsonValueKind.Object)
                delta.Usage = ParseUsage(usage);

            // 角色等非文本增量不向下游转发
            if (delta.Content == null && delta.FinishReason == null && delta.Usage == null && delta.Model == null) return null;

            if (delta.FinishReason != null) delta.Elapsed = elapsed;

            return delta;
        }
    }

    private static AIChatUsage ParseUsage(JsonElement usage) => new()
    {
        InputTokens = GetInt32(usage, "prompt_tokens"),
        OutputTokens = GetInt32(usage, "completion_tokens"),
        TotalTokens = GetInt32(usage, "total_tokens"),
    };

    /// <summary>提取上游错误信息。优先取 error.message，其次截取响应正文</summary>
    /// <param name="body">响应文本</param>
    /// <returns>可供日志与调用方阅读的错误摘要</returns>
    private static String GetError(String? body)
    {
        if (body.IsNullOrWhiteSpace()) return "no response body";

        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                doc.RootElement.TryGetProperty("error", out var error))
            {
                if (error.ValueKind == JsonValueKind.Object && error.TryGetProperty("message", out var message))
                    return message.GetString() ?? "";
                if (error.ValueKind == JsonValueKind.String)
                    return error.GetString() ?? "";
            }
        }
        catch (JsonException) { }

        return body.Length > 200 ? body[..200] : body;
    }

    private static Int32 GetInt32(JsonElement element, String name) =>
        element.TryGetProperty(name, out var value) && value.TryGetInt32(out var number) ? number : 0;
    #endregion
}