using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NewLife;
using NewLife.Cube;
using NewLife.Cube.ViewModels;
using NewLife.Log;
using Stardust.Web.Services;

namespace Stardust.Web.Areas.Deployment.Controllers;

/// <summary>流水线 webhook 接入。无鉴权，仅靠 token + 可选 secret。
/// 接收 git push JSON 负载，委托 PipelineService.HandleWebhookAsync 处理</summary>
[DeploymentArea]
[Route("[area]/[controller]/[action]")]
public class PipelineWebhookController(PipelineService pipelineService, ITracer tracer) : ControllerBaseX
{
    /// <summary>接收 git push webhook。兼容源 MVC 版对外地址 /Deployment/Pipeline/Webhook</summary>
    /// <param name="token">流水线鉴权 token</param>
    [HttpPost]
    [Route("/Deployment/Pipeline/Webhook")]
    [AllowAnonymous]
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

    /// <summary>获取字段信息。webhook 接收器无关联实体，仅返回 token/secret 鉴权参数说明，供前端构造 webhook URL 与签名配置参考</summary>
    /// <param name="kind">字段类型：1-列表List、2-详情Detail、3-添加AddForm、4-编辑EditForm、5-搜索Search</param>
    /// <returns></returns>
    [AllowAnonymous]
    [HttpGet]
    public virtual List<DataField> GetFields(ViewKinds kind)
    {
        // webhook 接收器无关联实体，仅返回 token/secret 鉴权参数说明，
        // 供前端构造 webhook URL 与签名配置参考。返回统一 ApiResponse 契约的字段列表。
        return new List<DataField>
        {
            new() { Name = "token", DisplayName = "Token", Description = "流水线鉴权 token，拼接到 webhook URL 的 ?token= 参数" },
            new() { Name = "secret", DisplayName = "Secret", Description = "可选，HMAC-SHA256 签名密钥（X-Hub-Signature-256）" },
        };
    }
}
