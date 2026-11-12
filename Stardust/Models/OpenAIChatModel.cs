using System.Runtime.Serialization;

namespace Stardust.Models;

/// <summary>OpenAI 对话消息</summary>
public class OpenAIChatMessage
{
    /// <summary>角色。system/user/assistant</summary>
    [DataMember(Name = "role")]
    public String? Role { get; set; }

    /// <summary>消息内容</summary>
    [DataMember(Name = "content")]
    public String? Content { get; set; }
}

/// <summary>OpenAI 兼容对话请求</summary>
/// <remarks>仅实现 Chat Completions 的常用字段；未识别字段由反序列化忽略</remarks>
public class OpenAIChatRequest
{
    /// <summary>模型编码。为空时使用平台默认模型</summary>
    [DataMember(Name = "model")]
    public String? Model { get; set; }

    /// <summary>消息列表</summary>
    [DataMember(Name = "messages")]
    public List<OpenAIChatMessage>? Messages { get; set; }

    /// <summary>采样温度</summary>
    [DataMember(Name = "temperature")]
    public Double? Temperature { get; set; }

    /// <summary>最大输出令牌数</summary>
    [DataMember(Name = "max_tokens")]
    public Int32? MaxTokens { get; set; }

    /// <summary>是否流式。true 时以 SSE 逐段返回</summary>
    [DataMember(Name = "stream")]
    public Boolean? Stream { get; set; }

    /// <summary>调用方标识。透传给上游</summary>
    [DataMember(Name = "user")]
    public String? User { get; set; }
}

/// <summary>OpenAI 兼容对话响应。非流式为完整消息，流式时作为分片</summary>
public class OpenAIChatResponse
{
    /// <summary>响应编号</summary>
    [DataMember(Name = "id")]
    public String? Id { get; set; }

    /// <summary>对象类型。chat.completion 或 chat.completion.chunk</summary>
    [DataMember(Name = "object")]
    public String? Object { get; set; }

    /// <summary>创建时间。Unix 秒</summary>
    [DataMember(Name = "created")]
    public Int64 Created { get; set; }

    /// <summary>实际使用的模型编码</summary>
    [DataMember(Name = "model")]
    public String? Model { get; set; }

    /// <summary>候选回复列表</summary>
    [DataMember(Name = "choices")]
    public List<OpenAIChatChoice>? Choices { get; set; }

    /// <summary>令牌用量</summary>
    [DataMember(Name = "usage")]
    public OpenAIUsage? Usage { get; set; }

    /// <summary>错误信息。流式错误分片使用</summary>
    [DataMember(Name = "error")]
    public OpenAIError? Error { get; set; }
}

/// <summary>OpenAI 对话候选</summary>
public class OpenAIChatChoice
{
    /// <summary>候选序号</summary>
    [DataMember(Name = "index")]
    public Int32 Index { get; set; }

    /// <summary>完整回复消息。非流式使用</summary>
    [DataMember(Name = "message")]
    public OpenAIChatMessage? Message { get; set; }

    /// <summary>增量消息。流式使用</summary>
    [DataMember(Name = "delta")]
    public OpenAIChatMessage? Delta { get; set; }

    /// <summary>结束原因。stop/length/content_filter 等</summary>
    [DataMember(Name = "finish_reason")]
    public String? FinishReason { get; set; }
}

/// <summary>OpenAI 令牌用量</summary>
public class OpenAIUsage
{
    /// <summary>输入令牌数</summary>
    [DataMember(Name = "prompt_tokens")]
    public Int32 PromptTokens { get; set; }

    /// <summary>输出令牌数</summary>
    [DataMember(Name = "completion_tokens")]
    public Int32 CompletionTokens { get; set; }

    /// <summary>总令牌数</summary>
    [DataMember(Name = "total_tokens")]
    public Int32 TotalTokens { get; set; }
}

/// <summary>OpenAI 兼容错误响应</summary>
public class OpenAIErrorResponse
{
    /// <summary>错误详情</summary>
    [DataMember(Name = "error")]
    public OpenAIError? Error { get; set; }
}

/// <summary>OpenAI 兼容错误详情</summary>
public class OpenAIError
{
    /// <summary>错误消息</summary>
    [DataMember(Name = "message")]
    public String? Message { get; set; }

    /// <summary>错误类型。invalid_request_error/authentication_error/permission_error/server_error</summary>
    [DataMember(Name = "type")]
    public String? Type { get; set; }

    /// <summary>错误参数</summary>
    [DataMember(Name = "param")]
    public String? Param { get; set; }

    /// <summary>错误码</summary>
    [DataMember(Name = "code")]
    public String? Code { get; set; }
}
