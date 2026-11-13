namespace Stardust.Server.Services;

/// <summary>AI对话上下文。由应用登录令牌解析出的应用与团队信息，注入系统提示词供大模型参考</summary>
/// <remarks>这些信息来自平台数据库，不来自客户端，客户端无法伪造；仅在服务端组装上游请求时使用</remarks>
public class AIChatContext
{
    /// <summary>应用编码</summary>
    public String? AppName { get; set; }

    /// <summary>应用显示名</summary>
    public String? DisplayName { get; set; }

    /// <summary>应用类别</summary>
    public String? Category { get; set; }

    /// <summary>应用版本</summary>
    public String? Version { get; set; }

    /// <summary>团队（星系项目）名称</summary>
    public String? TeamName { get; set; }
}
