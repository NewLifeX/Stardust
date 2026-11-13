using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;
using Microsoft.AspNetCore.Mvc;
using NewLife;
using NewLife.Security;
using Stardust.Models;
using Stardust.Server.Services;

namespace Stardust.Server.Controllers;

/// <summary>OpenAI 兼容流式对话结果。以 SSE 逐段写出 chat.completion.chunk</summary>
/// <remarks>
/// 每段一条 <c>data: {json}</c> 事件，结束写 <c>data: [DONE]</c>。
/// 上游或参数异常时：响应未开始则回退为标准 OpenAI 错误 JSON；已开始则写一条错误 data 事件后结束。
/// </remarks>
/// <param name="service">AI对话服务</param>
/// <param name="request">内部对话请求</param>
/// <param name="appId">调用方应用标识</param>
/// <param name="ip">调用方地址</param>
/// <param name="model">请求指定的模型编码</param>
/// <param name="appContext">应用与团队上下文，作为系统消息注入上游</param>
public sealed class OpenAIStreamResult(AIService service, AIChatRequest request, String? appId, String? ip, String? model, AIChatContext? appContext) : IActionResult
{
    private static readonly JsonSerializerOptions _options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
    };

    /// <summary>执行结果，写出SSE流</summary>
    /// <param name="context">动作上下文</param>
    /// <returns></returns>
    public async Task ExecuteResultAsync(ActionContext context)
    {
        var http = context.HttpContext;
        var response = http.Response;
        var cancellationToken = http.RequestAborted;

        response.ContentType = "text/event-stream; charset=utf-8";
        response.Headers.CacheControl = "no-cache";
        response.Headers["X-Accel-Buffering"] = "no";

        var id = "chatcmpl-" + Rand.NextString(24);
        var created = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var code = model + "";

        try
        {
            var hasRole = false;
            await foreach (var delta in service.StreamAsync(request, appId, ip, appContext, cancellationToken))
            {
                if (!delta.Model.IsNullOrEmpty()) code = delta.Model;

                // 首包先下发角色，符合 OpenAI 流式响应约定
                if (!hasRole)
                {
                    hasRole = true;
                    await WriteChunkAsync(response, id, created, code, new OpenAIChatMessage { Role = "assistant" }, null, null, cancellationToken).ConfigureAwait(false);
                }

                if (delta.Content != null || delta.FinishReason != null)
                {
                    var message = delta.Content == null ? new OpenAIChatMessage() : new OpenAIChatMessage { Content = delta.Content };
                    await WriteChunkAsync(response, id, created, code, message, delta.FinishReason, null, cancellationToken).ConfigureAwait(false);
                }
                if (delta.Usage != null)
                {
                    var usage = new OpenAIUsage
                    {
                        PromptTokens = delta.Usage.InputTokens,
                        CompletionTokens = delta.Usage.OutputTokens,
                        TotalTokens = delta.Usage.TotalTokens,
                    };
                    await WriteChunkAsync(response, id, created, code, null, null, usage, cancellationToken).ConfigureAwait(false);
                }
            }

            if (!hasRole)
                await WriteChunkAsync(response, id, created, code, new OpenAIChatMessage { Role = "assistant" }, null, null, cancellationToken).ConfigureAwait(false);

            await WriteDataAsync(response, "[DONE]", cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // 客户端断开，无需再写响应
        }
        catch (Exception ex)
        {
            var (status, message, type, errorCode) = OpenAIErrors.Map(ex);
            var json = JsonSerializer.Serialize(new OpenAIErrorResponse
            {
                Error = new OpenAIError { Message = message, Type = type, Code = errorCode },
            }, _options);

            if (!response.HasStarted)
            {
                // 尚未写出任何数据，回退为标准JSON错误
                response.StatusCode = status;
                response.ContentType = "application/json; charset=utf-8";
                await response.WriteAsync(json, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                // 已经进入流式输出，只能通过错误事件告知
                await WriteDataAsync(response, json, cancellationToken).ConfigureAwait(false);
                await WriteDataAsync(response, "[DONE]", cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary>写一个流式分片并立即刷新</summary>
    /// <param name="response">响应</param>
    /// <param name="id">响应编号</param>
    /// <param name="created">创建时间</param>
    /// <param name="model">模型编码</param>
    /// <param name="delta">增量消息。为空表示用量分片</param>
    /// <param name="finishReason">结束原因</param>
    /// <param name="usage">令牌用量</param>
    /// <param name="cancellationToken">取消令牌</param>
    private static Task WriteChunkAsync(HttpResponse response, String id, Int64 created, String? model, OpenAIChatMessage? delta, String? finishReason, OpenAIUsage? usage, CancellationToken cancellationToken)
    {
        var chunk = new OpenAIChatResponse
        {
            Id = id,
            Object = "chat.completion.chunk",
            Created = created,
            Model = model,
            Choices = delta == null ? [] : [new OpenAIChatChoice { Index = 0, Delta = delta, FinishReason = finishReason }],
            Usage = usage,
        };

        return WriteDataAsync(response, JsonSerializer.Serialize(chunk, _options), cancellationToken);
    }

    /// <summary>写一条 data 事件并刷新</summary>
    /// <param name="response">响应</param>
    /// <param name="data">事件数据</param>
    /// <param name="cancellationToken">取消令牌</param>
    private static async Task WriteDataAsync(HttpResponse response, String data, CancellationToken cancellationToken)
    {
        await response.WriteAsync($"data: {data}\n\n", cancellationToken).ConfigureAwait(false);
        await response.Body.FlushAsync(cancellationToken).ConfigureAwait(false);
    }
}
