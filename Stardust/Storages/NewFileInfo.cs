namespace Stardust.Storages;

/// <summary>
/// 新文件消息：宣告指定附件在某个节点可用。
/// </summary>
public class NewFileInfo
{
    /// <summary>公共数据库中的附件ID。</summary>
    public Int64 AttachmentId { get; set; }

    /// <summary>当前文件所在的逻辑节点名称。</summary>
    public String? SourceNode { get; set; }

    /// <summary>建议消费者保存的相对路径（可选）。</summary>
    public String? RelativePath { get; set; }

    /// <summary>文件内容哈希（如SHA256），用于快速验证（可选）。</summary>
    public String? Hash { get; set; }

    /// <summary>MIME类型（可选）。</summary>
    public String? ContentType { get; set; }

    /// <summary>文件大小（字节）（可选）。</summary>
    public Int64? Length { get; set; }

    /// <summary>消息创建的UTC时间戳。</summary>
    public DateTimeOffset Created { get; set; } = DateTimeOffset.UtcNow;
}
