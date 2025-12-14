namespace Stardust.Storages;

/// <summary>
/// 文件请求消息：请求其他节点提供指定附件文件。
/// </summary>
public class FileRequest
{
    /// <summary>公共数据库中的附件ID。</summary>
    public Int64 Id { get; set; }

    /// <summary>名称</summary>
    public String? Name { get; set; }

    /// <summary>相对路径</summary>
    public String? Path { get; set; }

    /// <summary>文件内容哈希（如SHA256），用于快速验证</summary>
    public String? Hash { get; set; }

    /// <summary>请求原因（用于诊断）（可选）。</summary>
    public String? Reason { get; set; }

    /// <summary>发起请求的逻辑节点名称。</summary>
    public String? RequestNode { get; set; }
}
