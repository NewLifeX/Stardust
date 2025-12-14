namespace Stardust.Storages;

/// <summary>文件信息接口</summary>
public interface IFileInfo
{
    /// <summary>公共数据库中的附件ID。</summary>
    Int64 Id { get; set; }

    /// <summary>文件名称</summary>
    String? Name { get; set; }

    /// <summary>相对路径</summary>
    String? Path { get; set; }

    /// <summary>文件内容哈希（如SHA256），用于快速验证</summary>
    String? Hash { get; set; }

    /// <summary>文件大小（字节）</summary>
    Int64 Length { get; set; }
}