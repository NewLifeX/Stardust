using System.Linq;

namespace Stardust.Data.Deployment;

/// <summary>webhook 解析器注册表（策略选择）。按注册顺序匹配平台，由自定义解析器兜底。
/// 新增 git 平台只需实现 IWebhookParser 并在 Parsers 中追加一项，流水线后续流程不变。</summary>
public static class WebhookParserRegistry
{
    /// <summary>解析器集合。顺序即优先级，兜底解析器必须放在最后</summary>
    public static IWebhookParser[] Parsers { get; } =
    {
        new GithubWebhookParser(),
        new GitlabWebhookParser(),
        new GiteeWebhookParser(),
        new GiteaWebhookParser(),
        new StardustWebhookParser(),
    };

    /// <summary>选择解析器</summary>
    /// <param name="request">webhook 入参</param>
    /// <returns>匹配的解析器，未注册时返回 null</returns>
    public static IWebhookParser Resolve(WebhookRequest request)
    {
        if (request == null) return null;

        return Parsers.FirstOrDefault(e => e.Match(request));
    }

    /// <summary>解析为规范化推送事件</summary>
    /// <param name="request">webhook 入参</param>
    /// <returns>统一模型，无法解析时返回 null</returns>
    public static WebhookPushEvent Parse(WebhookRequest request) => Resolve(request)?.Parse(request);
}
