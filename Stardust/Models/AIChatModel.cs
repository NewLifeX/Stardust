namespace Stardust.Models;

/// <summary>AI对话消息</summary>
public class AIChatMessage
{
    /// <summary>角色。system/user/assistant，默认 user</summary>
    public String Role { get; set; } = "user";

    /// <summary>消息内容</summary>
    public String Content { get; set; } = null!;
}

/// <summary>AI对话请求。一次请求携带完整上下文，服务端一次性返回完整回复</summary>
/// <remarks>
/// 不支持流式输出。需要多轮对话时，由调用方把历史消息按顺序放进 <see cref="Messages"/>，
/// 服务端不保存会话状态；系统提示词由 <see cref="SystemPrompt"/> 或平台默认配置提供。
/// </remarks>
public class AIChatRequest
{
    /// <summary>消息列表。按时间顺序排列，通常最后一条为 user</summary>
    public List<AIChatMessage> Messages { get; set; } = [];

    /// <summary>模型编码。为空时使用服务端默认模型</summary>
    public String? Model { get; set; }

    /// <summary>系统提示词。非空且消息列表中没有 system 消息时插入到最前面，并覆盖服务端默认提示词</summary>
    public String? SystemPrompt { get; set; }

    /// <summary>采样温度。0~2，为空时使用服务端默认值</summary>
    public Double? Temperature { get; set; }

    /// <summary>最大输出令牌数。为空时使用服务端默认值</summary>
    public Int32? MaxTokens { get; set; }
}

/// <summary>AI对话用量</summary>
public class AIChatUsage
{
    /// <summary>输入令牌数</summary>
    public Int32 InputTokens { get; set; }

    /// <summary>输出令牌数</summary>
    public Int32 OutputTokens { get; set; }

    /// <summary>总令牌数</summary>
    public Int32 TotalTokens { get; set; }
}

/// <summary>AI对话响应</summary>
public class AIChatResponse
{
    /// <summary>回复内容</summary>
    public String? Content { get; set; }

    /// <summary>实际使用的模型编码</summary>
    public String? Model { get; set; }

    /// <summary>结束原因。stop/length/content_filter 等</summary>
    public String? FinishReason { get; set; }

    /// <summary>令牌用量。上游未返回时为空</summary>
    public AIChatUsage? Usage { get; set; }

    /// <summary>服务端耗时。单位毫秒</summary>
    public Int64 Elapsed { get; set; }
}

/// <summary>AI流式对话增量</summary>
/// <remarks>流式响应按 SSE 逐段下发，每段只带本次新增的文本；结束段带 <see cref="FinishReason"/></remarks>
public class AIChatDelta
{
    /// <summary>本次增量文本</summary>
    public String? Content { get; set; }

    /// <summary>实际使用的模型编码。通常出现在首段</summary>
    public String? Model { get; set; }

    /// <summary>结束原因。结束段给出，为空表示对话尚未结束</summary>
    public String? FinishReason { get; set; }

    /// <summary>令牌用量。上游在流式模式下通常不返回</summary>
    public AIChatUsage? Usage { get; set; }

    /// <summary>服务端耗时。结束段给出，单位毫秒</summary>
    public Int64 Elapsed { get; set; }
}
