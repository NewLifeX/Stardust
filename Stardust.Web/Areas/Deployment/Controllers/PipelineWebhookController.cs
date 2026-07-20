using Microsoft.AspNetCore.Mvc;
using NewLife;
using NewLife.Log;
using Stardust.Web.Services;

namespace Stardust.Web.Areas.Deployment.Controllers;

/// <summary>流水线 webhook 接入。无鉴权，仅靠 token + 可选 secret。
/// 接收 git push JSON 负载，委托 PipelineService.HandleWebhookAsync 处理</summary>
[DeploymentArea]
[Route("[area]/Pipeline/[action]")]
public class PipelineWebhookController(PipelineService pipelineService, ITracer tracer) : ControllerBase
{
    /// <summary>接收 git push webhook</summary>
    /// <param name="token">流水线鉴权 token</param>
    [HttpPost]
    public async Task<Object> Webhook(String token)
    {
        if (token.IsNullOrEmpty()) return new { result = "error", reason = "missing token" };

        // 读取原始 body 用于签名校验
        Request.EnableBuffering();
        Request.Body.Position = 0;
        using var reader = new StreamReader(Request.Body, leaveOpen: true);
        var body = await reader.ReadToEndAsync();
        Request.Body.Position = 0;

        var signature = Request.Headers["X-Hub-Signature-256"].ToString();

        using var span = tracer?.NewSpan("Pipeline-Webhook", new { token, len = body?.Length ?? 0 });
        var rs = await pipelineService.HandleWebhookAsync(token, body, signature);
        span?.AppendTag($"result={rs}");
        return rs;
    }
}
