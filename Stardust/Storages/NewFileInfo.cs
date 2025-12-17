using NewLife.Log;

namespace Stardust.Storages;

/// <summary>
/// 新文件消息：宣告指定附件在某个节点可用。
/// </summary>
public class NewFileInfo : IFileInfo, ITraceMessage
{
    /// <summary>公共数据库中的附件ID。</summary>
    public Int64 Id { get; set; }

    /// <summary>文件名称</summary>
    public String? Name { get; set; }

    /// <summary>相对路径</summary>
    public String? Path { get; set; }

    /// <summary>文件内容哈希（如SHA256），用于快速验证</summary>
    public String? Hash { get; set; }

    /// <summary>文件大小（字节）</summary>
    public Int64 Length { get; set; }

    /// <summary>当前文件所在的逻辑节点名称。</summary>
    public String? SourceNode { get; set; }

    /// <summary>内网地址</summary>
    public String? InternalAddress { get; set; }

    /// <summary>外网地址</summary>
    public String? ExternalAddress { get; set; }

    /// <summary>追踪标识</summary>
    public String? TraceId { get; set; }
}
