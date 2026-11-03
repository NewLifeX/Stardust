using System;
using NewLife;

namespace Stardust.Data.Deployment;

/// <summary>webhook 原始入参（适配器的输入）。把各平台差异（事件头、签名头、令牌头）收敛为统一结构</summary>
public class WebhookRequest
{
    /// <summary>原始请求体</summary>
    public String Body { get; set; }

    /// <summary>平台标识。由事件头识别，github/gitlab/gitee/gitea，未识别时为空</summary>
    public String Platform { get; set; }

    /// <summary>平台事件名</summary>
    public String Event { get; set; }

    /// <summary>签名摘要。GitHub/Gitea 以请求头携带 HMAC 摘要</summary>
    public String Signature { get; set; }

    /// <summary>平台令牌。GitLab/Gitee 以请求头直接携带明文令牌</summary>
    public String Token { get; set; }

    /// <summary>按请求头构造入参。各平台事件头名互斥，据此识别平台与事件名</summary>
    /// <param name="body">原始请求体</param>
    /// <param name="getHeader">取请求头委托，头部缺失时返回空</param>
    /// <returns>统一的 webhook 入参</returns>
    public static WebhookRequest Create(String body, Func<String, String> getHeader)
    {
        var request = new WebhookRequest { Body = body };
        if (getHeader == null) return request;

        // 事件头：头名本身即平台标识，避免 GitLab 与 Gitee 的事件名同为 Push Hook 而无法区分
        var value = getHeader("X-GitHub-Event");
        if (!value.IsNullOrEmpty()) { request.Platform = "github"; request.Event = value; }
        value = getHeader("X-Gitlab-Event");
        if (!value.IsNullOrEmpty()) { request.Platform = "gitlab"; request.Event = value; }
        value = getHeader("X-Gitee-Event");
        if (!value.IsNullOrEmpty()) { request.Platform = "gitee"; request.Event = value; }
        value = getHeader("X-Gitea-Event");
        if (!value.IsNullOrEmpty()) { request.Platform = "gitea"; request.Event = value; }

        // 签名头：HMAC 摘要（GitHub 新版优先）
        value = getHeader("X-Hub-Signature-256");
        if (value.IsNullOrEmpty()) value = getHeader("X-Hub-Signature");
        if (value.IsNullOrEmpty()) value = getHeader("X-Gitea-Signature");
        request.Signature = value;

        // 令牌头：明文比对
        value = getHeader("X-Gitlab-Token");
        if (value.IsNullOrEmpty()) value = getHeader("X-Gitee-Token");
        request.Token = value;

        return request;
    }
}
