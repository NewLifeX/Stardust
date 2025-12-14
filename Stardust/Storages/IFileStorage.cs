namespace Stardust.Storages;

/// <summary>描述在多节点间传播附件文件所需的操作</summary>
public interface IFileStorage : IDisposable
{
    /// <summary>当前节点的逻辑名称。</summary>
    String? NodeName { get; set; }

    /// <summary>确保实现类已订阅配置的事件总线。</summary>
    Task InitializeAsync(CancellationToken cancellationToken = default);

    /// <summary>广播指定附件在当前节点可用。</summary>
    Task PublishNewFileAsync(Int64 attachmentId, String? path, CancellationToken cancellationToken = default);

    /// <summary>发布文件请求，向其他节点索取指定附件。</summary>
    Task RequestFileAsync(Int64 attachmentId, String? path, String? reason = null, CancellationToken cancellationToken = default);

    /// <summary>扫描并批量请求缺失附件，返回发出请求的数量。</summary>
    Task<Int32> ScanFilesAsync(Int32 batchSize = 50, CancellationToken cancellationToken = default);
}
