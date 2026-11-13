using System.Net;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;
using Microsoft.AspNetCore.Mvc;
using NewLife;
using NewLife.Log;
using NewLife.Remoting.Extensions;
using NewLife.Remoting.Services;
using NewLife.Security;
using Stardust.Data;
using Stardust.Data.Platform;
using Stardust.Models;
using Stardust.Server.Services;

namespace Stardust.Server.Controllers;

/// <summary>OpenAI 兼容对话接口。下游应用用星尘登录令牌作为 api_key 调用</summary>
/// <remarks>
/// 单端点 <c>POST /v1/chat/completions</c>，通过请求体 <c>stream</c> 开关切换流式与非流式：
/// 非流式返回标准 chat.completion，流式以 SSE 逐段返回 chat.completion.chunk，结束标记 data: [DONE]。
/// 鉴权使用 <c>Authorization: Bearer {应用登录令牌}</c>，与星尘普通接口共用同一枚令牌。
/// </remarks>
[ApiController]
[Route("v1")]
public class OpenAIController(AIService aiService, ITokenService tokenService) : ControllerBase
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
    };

    /// <summary>对话。按 OpenAI Chat Completions 协议，stream 开关切换流式与非流式</summary>
    /// <param name="model">对话请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>非流式返回 chat.completion；流式返回 text/event-stream</returns>
    [HttpPost("chat/completions")]
    public async Task<IActionResult> ChatCompletions([FromBody] OpenAIChatRequest? model, CancellationToken cancellationToken)
    {
        var app = Valid(GetToken());
        if (app == null) return Error(HttpStatusCode.Unauthorized, L.T("Invalid application token"), "authentication_error", "invalid_api_key");
        if (!app.Enable) return Error(HttpStatusCode.Forbidden, L.T("Application [{0}] is disabled.", app.Name), "permission_error", "app_disabled");
        if (model?.Messages == null || model.Messages.Count == 0) return Error(HttpStatusCode.BadRequest, L.T("AI request contains no valid message."), "invalid_request_error", "invalid_request");

        // 转换为内部请求模型，复用统一的开关/配置校验、模型白名单与系统提示词注入
        var request = new AIChatRequest
        {
            Model = model.Model,
            Temperature = model.Temperature,
            MaxTokens = model.MaxTokens,
            Messages = model.Messages.Select(e => new AIChatMessage
            {
                Role = e.Role.IsNullOrWhiteSpace() ? "user" : e.Role.Trim(),
                Content = e.Content + "",
            }).ToList(),
        };
        var ip = HttpContext.GetUserHost();
        var appContext = CreateContext(app);

        if (model.Stream == true) return new OpenAIStreamResult(aiService, request, app.Name, ip, model.Model, appContext);

        try
        {
            var rs = await aiService.ChatAsync(request, app.Name, ip, appContext, cancellationToken).ConfigureAwait(false);
            return JsonContent(ToResponse(rs, model.Model));
        }
        catch (Exception ex)
        {
            // 异常统一转换为 OpenAI 错误体，避免下游按 OpenAI 协议解析时收到星尘的 {code,data} 结构
            var (status, message, type, code) = OpenAIErrors.Map(ex);
            return Error((HttpStatusCode)status, message, type, code);
        }
    }

    #region 辅助
    /// <summary>从请求头获取访问令牌。兼容 Authorization: Bearer 与 X-Token</summary>
    /// <returns>访问令牌，未携带时返回空</returns>
    private String? GetToken()
    {
        var auth = Request.Headers.Authorization + "";
        if (auth.StartsWithIgnoreCase("Bearer ")) return auth[7..].Trim();

        var token = Request.Headers["X-Token"] + "";
        return token.IsNullOrEmpty() ? null : token.Trim();
    }

    /// <summary>校验应用令牌。令牌主体必须存在且为已启用的应用</summary>
    /// <param name="token">访问令牌</param>
    /// <returns>令牌对应的应用；无效时返回空</returns>
    private App? Valid(String? token)
    {
        if (token.IsNullOrEmpty()) return null;

        var (jwt, ex) = tokenService.DecodeToken(token);
        if (ex != null) return null;

        return App.FindByName(jwt?.Subject);
    }

    /// <summary>组装应用与团队上下文。团队名称来自应用所属星系项目，供注入系统提示词</summary>
    /// <param name="app">登录令牌对应的应用</param>
    /// <returns>对话上下文</returns>
    private static AIChatContext CreateContext(App app)
    {
        var project = app.ProjectId > 0 ? GalaxyProject.FindById(app.ProjectId) : null;

        return new AIChatContext
        {
            AppName = app.Name,
            DisplayName = app.DisplayName,
            Category = app.Category,
            Version = app.Version,
            TeamName = project?.Name,
        };
    }

    /// <summary>转换为 OpenAI 非流式响应</summary>
    /// <param name="rs">内部对话响应</param>
    /// <param name="model">请求指定的模型编码</param>
    /// <returns>OpenAI 兼容响应</returns>
    private static OpenAIChatResponse ToResponse(AIChatResponse rs, String? model) => new()
    {
        Id = "chatcmpl-" + Rand.NextString(24),
        Object = "chat.completion",
        Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
        Model = rs.Model ?? model,
        Choices =
        [
            new OpenAIChatChoice
            {
                Index = 0,
                Message = new OpenAIChatMessage { Role = "assistant", Content = rs.Content },
                FinishReason = rs.FinishReason ?? "stop",
            },
        ],
        Usage = new OpenAIUsage
        {
            PromptTokens = rs.Usage?.InputTokens ?? 0,
            CompletionTokens = rs.Usage?.OutputTokens ?? 0,
            TotalTokens = rs.Usage?.TotalTokens ?? 0,
        },
    };

    private IActionResult JsonContent(OpenAIChatResponse response) => new ContentResult
    {
        StatusCode = (Int32)HttpStatusCode.OK,
        ContentType = "application/json; charset=utf-8",
        Content = JsonSerializer.Serialize(response, _jsonOptions),
    };

    private IActionResult Error(HttpStatusCode status, String message, String type, String? code) => new ContentResult
    {
        StatusCode = (Int32)status,
        ContentType = "application/json; charset=utf-8",
        Content = JsonSerializer.Serialize(new OpenAIErrorResponse
        {
            Error = new OpenAIError { Message = message, Type = type, Code = code },
        }, _jsonOptions),
    };
    #endregion
}
