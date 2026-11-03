using System;
using System.Linq;
using System.Runtime.Serialization;
using NewLife;
using NewLife.Serialization;

namespace Stardust.Data.Deployment;

/// <summary>GitHub 系 webhook 解析器。GitHub/Gitee/Gitea 的 push 负载同构：
/// ref/before/after/commits/head_commit/repository/pusher/sender，差异只在事件头与签名方式。
/// 字段同时兼容蛇形（head_commit）、小驼峰（headCommit）与大驼峰（HeadCommit）</summary>
public class GithubStyleWebhookParser : IWebhookParser
{
    /// <summary>平台标识</summary>
    public virtual String Name => "github";

    /// <summary>是否匹配。平台由事件头识别，事件名需为 push</summary>
    /// <param name="request">webhook 入参</param>
    /// <returns>是否匹配</returns>
    public virtual Boolean Match(WebhookRequest request)
        => request != null && request.Platform.EqualIgnoreCase(Name) && IsPushEvent(request.Event);

    /// <summary>解析为规范化推送事件</summary>
    /// <param name="request">webhook 入参</param>
    /// <returns>统一模型</returns>
    public virtual WebhookPushEvent Parse(WebhookRequest request) => ParsePayload(request, Name);

    /// <summary>是否推送事件。事件头缺失时按推送处理</summary>
    /// <param name="evt">事件名</param>
    /// <returns>是否推送事件</returns>
    protected static Boolean IsPushEvent(String evt)
        => evt.IsNullOrEmpty() || evt.EqualIgnoreCase("push") || evt.EqualIgnoreCase("push hook");

    /// <summary>解析 GitHub 系负载</summary>
    /// <param name="request">webhook 入参</param>
    /// <param name="platform">平台标识</param>
    /// <returns>统一模型</returns>
    protected static WebhookPushEvent ParsePayload(WebhookRequest request, String platform)
    {
        if (request == null || request.Body.IsNullOrEmpty()) return null;

        var payload = request.Body.ToJsonEntity<GithubStylePayload>();
        if (payload == null) return null;

        var commits = payload.Commits;
        var head = payload.HeadCommit;
        var last = commits != null && commits.Length > 0 ? commits[commits.Length - 1] : null;

        var commitId = WebhookHelper.FirstNotEmpty(payload.After, head?.Id, last?.Id);
        var message = WebhookHelper.FirstNotEmpty(head?.Message, last?.Message);
        var author = WebhookHelper.FirstNotEmpty(
            head?.Author?.Name,
            head?.Author?.Username,
            payload.Pusher?.Name,
            payload.Pusher?.Username,
            last?.Author?.Name,
            payload.UserName);

        // 时间戳优先级：head_commit → 提交列表末条 → 顶层时间戳
        var time = head != null ? head.Timestamp : default;
        if (time == default && last != null) time = last.Timestamp;
        if (time == default) time = payload.Timestamp;

        return new WebhookPushEvent
        {
            Platform = platform,
            Event = request.Event,
            Ref = payload.Ref,
            Before = payload.Before,
            After = payload.After,
            Branch = WebhookHelper.GetBranch(payload.Ref),
            CommitId = commitId,
            CommitMessage = WebhookHelper.Cut(message, 500),
            CommitAuthor = WebhookHelper.Cut(author, 100),
            CommitTime = time,
            Repository = WebhookHelper.FirstNotEmpty(payload.Repository?.FullName, payload.Repository?.Name),
            Pusher = WebhookHelper.FirstNotEmpty(payload.Pusher?.Name, payload.Pusher?.Username, payload.Sender?.Name),
            Deleted = payload.Deleted == true || WebhookHelper.IsZeroSha(payload.After),
            Commits = commits?.Select(e => new WebhookCommitInfo
            {
                Id = e.Id,
                Message = e.Message,
                Author = WebhookHelper.FirstNotEmpty(e.Author?.Name, e.Author?.Username),
                Timestamp = e.Timestamp,
            }).ToArray(),
        };
    }
}

/// <summary>GitHub webhook 解析器</summary>
public class GithubWebhookParser : GithubStyleWebhookParser
{
    /// <summary>平台标识</summary>
    public override String Name => "github";
}

/// <summary>Gitea webhook 解析器</summary>
public class GiteaWebhookParser : GithubStyleWebhookParser
{
    /// <summary>平台标识</summary>
    public override String Name => "gitea";
}

/// <summary>Gitee webhook 解析器</summary>
public class GiteeWebhookParser : GithubStyleWebhookParser
{
    /// <summary>平台标识</summary>
    public override String Name => "gitee";
}

/// <summary>星尘自定义 webhook 解析器。作为兜底，兼容未带平台事件头的精简负载（headCommit 驼峰或 head_commit 蛇形）</summary>
public class StardustWebhookParser : GithubStyleWebhookParser
{
    /// <summary>平台标识</summary>
    public override String Name => "custom";

    /// <summary>兜底解析器，任何请求都接受</summary>
    /// <param name="request">webhook 入参</param>
    /// <returns>恒为 true</returns>
    public override Boolean Match(WebhookRequest request) => request != null;

    /// <summary>解析为规范化推送事件</summary>
    /// <param name="request">webhook 入参</param>
    /// <returns>统一模型</returns>
    public override WebhookPushEvent Parse(WebhookRequest request)
    {
        var platform = request?.Platform;
        return ParsePayload(request, platform.IsNullOrEmpty() ? Name : platform);
    }
}

/// <summary>GitHub 系 push 负载。GitHub/Gitee/Gitea 共用，蛇形字段通过 DataMember 别名绑定</summary>
internal class GithubStylePayload
{
    public String Ref { get; set; }
    public String Before { get; set; }
    public String After { get; set; }

    [DataMember(Name = "head_commit")]
    public GithubStyleCommit HeadCommit { get; set; }

    public GithubStyleCommit[] Commits { get; set; }
    public GithubStyleRepository Repository { get; set; }
    public GithubStyleUser Pusher { get; set; }
    public GithubStyleUser Sender { get; set; }

    /// <summary>删除引用标记。GitHub/Gitee 提供</summary>
    public Boolean? Deleted { get; set; }

    /// <summary>Gitee 钩子名</summary>
    [DataMember(Name = "hook_name")]
    public String HookName { get; set; }

    /// <summary>Gitee 顶层推送者昵称</summary>
    [DataMember(Name = "user_name")]
    public String UserName { get; set; }

    /// <summary>Gitee 顶层时间戳。毫秒数字，绑定失败时保持默认值</summary>
    public DateTime Timestamp { get; set; }
}

/// <summary>GitHub 系提交</summary>
internal class GithubStyleCommit
{
    public String Id { get; set; }
    public String Message { get; set; }
    public DateTime Timestamp { get; set; }
    public GithubStyleUser Author { get; set; }
    public GithubStyleUser Committer { get; set; }
}

/// <summary>GitHub 系仓库</summary>
internal class GithubStyleRepository
{
    public String Name { get; set; }

    [DataMember(Name = "full_name")]
    public String FullName { get; set; }
}

/// <summary>GitHub 系用户</summary>
internal class GithubStyleUser
{
    public String Name { get; set; }
    public String Username { get; set; }
    public String Login { get; set; }
}
