namespace Stardust.Storages;

/// <summary>
/// 文件请求消息：请求其他节点提供指定附件文件。
/// </summary>
public class FileRequest
{
    /// <summary>公共数据库中的附件ID。</summary>
    public Int64 AttachmentId { get; set; }

    /// <summary>请求原因（用于诊断）（可选）。</summary>
    public String? Reason { get; set; }

    /// <summary>发起请求的逻辑节点名称。</summary>
    public String? RequestNode { get; set; }

    /// <summary>期望的内容哈希（如SHA256），用于验证（可选）。</summary>
    public String? ExpectedHash { get; set; }

    /// <summary>建议消费者保存的相对路径（可选）。</summary>
    public String? RelativePath { get; set; }

    /// <summary>消息创建的UTC时间戳。</summary>
    public DateTimeOffset Created { get; set; } = DateTimeOffset.UtcNow;
}
