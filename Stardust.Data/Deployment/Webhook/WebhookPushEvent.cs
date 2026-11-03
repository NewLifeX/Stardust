using System;
using NewLife;

namespace Stardust.Data.Deployment;

/// <summary>规范化的推送事件（Canonical Model）。各 git 平台 webhook 经适配器解析后统一为本模型，
/// 流水线后续的校验、防抖、运行记录与编排只依赖本模型，新增平台只需新增一个 IWebhookParser 实现。</summary>
public class WebhookPushEvent
{
    /// <summary>平台标识。github/gitlab/gitee/gitea/custom</summary>
    public String Platform { get; set; }

    /// <summary>平台事件名。push、Push Hook 等</summary>
    public String Event { get; set; }

    /// <summary>完整引用。refs/heads/main 或 refs/tags/v1.0.0</summary>
    public String Ref { get; set; }

    /// <summary>推送前的提交编号</summary>
    public String Before { get; set; }

    /// <summary>推送后的提交编号</summary>
    public String After { get; set; }

    /// <summary>分支名。已剥离 refs/heads/ 前缀</summary>
    public String Branch { get; set; }

    /// <summary>本次运行的提交编号。优先 after，其次 checkout_sha/head_commit，最后提交列表末条</summary>
    public String CommitId { get; set; }

    /// <summary>提交信息</summary>
    public String CommitMessage { get; set; }

    /// <summary>提交人</summary>
    public String CommitAuthor { get; set; }

    /// <summary>提交时间</summary>
    public DateTime CommitTime { get; set; }

    /// <summary>仓库全名</summary>
    public String Repository { get; set; }

    /// <summary>推送者</summary>
    public String Pusher { get; set; }

    /// <summary>引用是否被删除。删除分支时 after 为全零</summary>
    public Boolean Deleted { get; set; }

    /// <summary>是否为标签推送</summary>
    public Boolean IsTag => !Ref.IsNullOrEmpty() && Ref.StartsWithIgnoreCase("refs/tags/");

    /// <summary>提交列表</summary>
    public WebhookCommitInfo[] Commits { get; set; }
}

/// <summary>推送事件中的提交信息</summary>
public class WebhookCommitInfo
{
    /// <summary>提交编号</summary>
    public String Id { get; set; }

    /// <summary>提交信息</summary>
    public String Message { get; set; }

    /// <summary>提交人</summary>
    public String Author { get; set; }

    /// <summary>提交时间</summary>
    public DateTime Timestamp { get; set; }
}
