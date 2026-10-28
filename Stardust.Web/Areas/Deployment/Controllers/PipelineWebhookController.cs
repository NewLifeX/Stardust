using Microsoft.AspNetCore.Mvc;
using NewLife;
using NewLife.Log;
using Stardust.Web.Services;

namespace Stardust.Web.Areas.Deployment.Controllers;

/// <summary>流水线 webhook 接入。无鉴权，仅靠 token + 可选 secret。
/// 接收 git push JSON 负载，委托 PipelineService.HandleWebhookAsync 处理</summary>
/// <remarks>
/// 这里只标 <see cref="AreaAttribute"/>，不用区域特有的 <see cref="DeploymentArea"/>：
/// 魔方按"是否带区域特性"扫描区域控制器的菜单，本控制器只有 Webhook 动作、没有管理页面，
/// 被扫描到就会生成一条点开即 404 的死菜单。路由模板里的 [area] 由 <see cref="AreaAttribute"/> 提供，功能不受影响。
/// </remarks>
[Area("Deployment")]
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
