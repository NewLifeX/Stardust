using System;
using System.Linq;
using System.Runtime.Serialization;
using NewLife;
using NewLife.Serialization;

namespace Stardust.Data.Deployment;

/// <summary>GitLab webhook 解析器。GitLab 的 push 负载与 GitHub 不同：
/// 无 head_commit，顶层给出 checkout_sha/message/user_name，提交明细在 commits</summary>
public class GitlabWebhookParser : IWebhookParser
{
    /// <summary>平台标识</summary>
    public String Name => "gitlab";

    /// <summary>是否匹配。GitLab 的 push 与 tag push 事件名均带 Hook 后缀</summary>
    /// <param name="request">webhook 入参</param>
    /// <returns>是否匹配</returns>
    public Boolean Match(WebhookRequest request)
        => request != null
        && request.Platform.EqualIgnoreCase(Name)
        && (request.Event.IsNullOrEmpty()
            || request.Event.EqualIgnoreCase("push")
            || request.Event.EqualIgnoreCase("Push Hook")
            || request.Event.EqualIgnoreCase("Tag Push Hook"));

    /// <summary>解析为规范化推送事件</summary>
    /// <param name="request">webhook 入参</param>
    /// <returns>统一模型</returns>
    public WebhookPushEvent Parse(WebhookRequest request)
    {
        if (request == null || request.Body.IsNullOrEmpty()) return null;

        var payload = request.Body.ToJsonEntity<GitlabPayload>();
        if (payload == null) return null;

        var commits = payload.Commits;
        var last = commits != null && commits.Length > 0 ? commits[commits.Length - 1] : null;

        var commitId = WebhookHelper.FirstNotEmpty(payload.After, payload.CheckoutSha, last?.Id);
        var message = WebhookHelper.FirstNotEmpty(last?.Message, last?.Title, payload.Message);
        var author = WebhookHelper.FirstNotEmpty(last?.Author?.Name, payload.UserName, payload.UserUsername);

        return new WebhookPushEvent
        {
            Platform = Name,
            Event = request.Event,
            Ref = payload.Ref,
            Before = payload.Before,
            After = payload.After,
            Branch = WebhookHelper.GetBranch(payload.Ref),
            CommitId = commitId,
            CommitMessage = WebhookHelper.Cut(message, 500),
            CommitAuthor = WebhookHelper.Cut(author, 100),
            CommitTime = last != null ? last.Timestamp : default,
            Repository = WebhookHelper.FirstNotEmpty(payload.Project?.PathWithNamespace, payload.Project?.Name, payload.Repository?.Name),
            Pusher = WebhookHelper.FirstNotEmpty(payload.UserName, payload.UserUsername),
            Deleted = WebhookHelper.IsZeroSha(payload.After),
            Commits = commits?.Select(e => new WebhookCommitInfo
            {
                Id = e.Id,
                Message = WebhookHelper.FirstNotEmpty(e.Message, e.Title),
                Author = e.Author?.Name,
                Timestamp = e.Timestamp,
            }).ToArray(),
        };
    }
}

/// <summary>GitLab push 负载。全部为蛇形字段，通过 DataMember 别名绑定</summary>
internal class GitlabPayload
{
    [DataMember(Name = "object_kind")]
    public String ObjectKind { get; set; }

    [DataMember(Name = "event_name")]
    public String EventName { get; set; }

    public String Ref { get; set; }
    public String Before { get; set; }
    public String After { get; set; }

    [DataMember(Name = "checkout_sha")]
    public String CheckoutSha { get; set; }

    [DataMember(Name = "ref_protected")]
    public Boolean RefProtected { get; set; }

    /// <summary>推送说明</summary>
    public String Message { get; set; }

    [DataMember(Name = "user_name")]
    public String UserName { get; set; }

    [DataMember(Name = "user_username")]
    public String UserUsername { get; set; }

    [DataMember(Name = "total_commits_count")]
    public Int32 TotalCommitsCount { get; set; }

    public GitlabProject Project { get; set; }
    public GitlabRepository Repository { get; set; }
    public GitlabCommit[] Commits { get; set; }
}

/// <summary>GitLab 项目</summary>
internal class GitlabProject
{
    public Int32 Id { get; set; }
    public String Name { get; set; }

    [DataMember(Name = "path_with_namespace")]
    public String PathWithNamespace { get; set; }

    [DataMember(Name = "default_branch")]
    public String DefaultBranch { get; set; }
}

/// <summary>GitLab 仓库</summary>
internal class GitlabRepository
{
    public String Name { get; set; }
    public String Url { get; set; }

    [DataMember(Name = "git_http_url")]
    public String GitHttpUrl { get; set; }

    [DataMember(Name = "git_ssh_url")]
    public String GitSshUrl { get; set; }
}

/// <summary>GitLab 提交</summary>
internal class GitlabCommit
{
    public String Id { get; set; }
    public String Message { get; set; }
    public String Title { get; set; }
    public DateTime Timestamp { get; set; }
    public String Url { get; set; }
    public GitlabUser Author { get; set; }
}

/// <summary>GitLab 用户</summary>
internal class GitlabUser
{
    public String Name { get; set; }
    public String Email { get; set; }
}
