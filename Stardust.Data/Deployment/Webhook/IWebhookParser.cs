using System;

namespace Stardust.Data.Deployment;

/// <summary>webhook 解析器（适配器）。每个 git 平台一个实现，负责识别请求并转换为统一的 WebhookPushEvent。
/// 新增平台时只需实现本接口并在 WebhookParserRegistry 中注册，流水线后续流程不变。</summary>
public interface IWebhookParser
{
    /// <summary>平台标识。github/gitlab/gitee/gitea/custom</summary>
    String Name { get; }

    /// <summary>是否由本解析器处理</summary>
    /// <param name="request">webhook 入参</param>
    /// <returns>是否匹配</returns>
    Boolean Match(WebhookRequest request);

    /// <summary>解析为规范化推送事件</summary>
    /// <param name="request">webhook 入参</param>
    /// <returns>统一模型，无法识别时返回 null</returns>
    WebhookPushEvent Parse(WebhookRequest request);
}
