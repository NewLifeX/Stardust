using NewLife;
using NewLife.Messaging;

namespace Stardust.Storages;

/// <summary>分布式文件存储默认基类，用于编排文件同步流程。具体应用应继承并实现与存储相关的操作。</summary>
public abstract class DefaultFileStorage : DisposeBase, IFileStorage
{
    #region 属性
    /// <summary>用于广播新文件消息的事件总线。</summary>
    public IEventBus<NewFileInfo>? NewFileBus { get; set; }

    /// <summary>用于发布文件请求消息的事件总线。</summary>
    public IEventBus<FileRequest>? FileRequestBus { get; set; }

    /// <summary>当前节点的逻辑名称。</summary>
    public String? NodeName { get; set; }

    private Int32 _initialized;

    #endregion

    #region 构造
    /// <summary>销毁释放</summary>
    /// <param name="disposing"></param>
    protected override void Dispose(Boolean disposing)
    {
        base.Dispose(disposing);

        NewFileBus.TryDispose();
        FileRequestBus.TryDispose();
    }
    #endregion

    #region 初始化
    /// <summary>初始化，确保订阅事件总线并设置处理逻辑。</summary>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref _initialized, 1) == 1) return;

        // 仅订阅，处理逻辑使用独立方法，避免捕获初始化的取消令牌
        NewFileBus?.Subscribe(OnNewFileInfoAsync);
        FileRequestBus?.Subscribe(OnFileRequestAsync);

        await OnInitializedAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>事件总线订阅完成后调用，覆写以执行额外初始化。</summary>
    protected virtual Task OnInitializedAsync(CancellationToken cancellationToken) => Task.FromResult(0);
    #endregion

    #region 新文件
    /// <summary>广播指定附件在当前节点可用。</summary>
    public async Task PublishNewFileAsync(Int64 attachmentId, CancellationToken cancellationToken = default)
    {
        if (NewFileBus == null) throw new InvalidOperationException("NewFileBus not configured.");

        var (hash, relPath, contentType, length) = GetLocalFileMeta(attachmentId);
        var msg = new NewFileInfo
        {
            AttachmentId = attachmentId,
            SourceNode = NodeName,
            Hash = hash,
            RelativePath = relPath,
            ContentType = contentType,
            Length = length
        };
        await NewFileBus.PublishAsync(msg, null, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>处理新文件消息。</summary>
    protected virtual async Task OnNewFileInfoAsync(NewFileInfo info)
    {
        // 默认忽略本节点自己发布的消息（除非需要自愈）
        if (info.SourceNode.EqualIgnoreCase(NodeName)) return;

        // 检查本地是否已有文件且哈希正确
        var exists = CheckLocalFile(info.AttachmentId, info.Hash);
        if (exists) return;

        // 从源节点拉取文件数据
        var data = await FetchFileFromNodeAsync(info.AttachmentId, info.SourceNode, CancellationToken.None).ConfigureAwait(false);
        if (data == null || data.Length == 0) return;

        // 校验并保存到本地
        ValidateAndSave(info.AttachmentId, data, info.Hash, info.RelativePath, info.ContentType);
    }

    /// <summary>通过应用自定义的传输方式（如HTTP接口）从指定源节点拉取文件数据。</summary>
    protected abstract Task<Byte[]?> FetchFileFromNodeAsync(Int64 attachmentId, String? sourceNode, CancellationToken cancellationToken);

    /// <summary>验证拉取到的文件数据并保存到本地存储。</summary>
    protected abstract Boolean ValidateAndSave(Int64 attachmentId, Byte[] data, String? expectedHash, String? relativePath, String? contentType);
    #endregion

    #region 文件请求
    /// <summary>发布请求，向其他节点索取指定附件。</summary>
    public async Task RequestFileAsync(Int64 attachmentId, String? reason = null, CancellationToken cancellationToken = default)
    {
        if (FileRequestBus == null) throw new InvalidOperationException("FileRequestBus not configured.");

        var (hash, relPath, _, _) = GetLocalFileMeta(attachmentId);
        var msg = new FileRequest
        {
            AttachmentId = attachmentId,
            Reason = reason,
            RequestNode = NodeName,
            ExpectedHash = hash,
            RelativePath = relPath
        };
        await FileRequestBus.PublishAsync(msg, null, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>处理文件请求消息。</summary>
    protected virtual async Task OnFileRequestAsync(FileRequest req)
    {
        if (req.RequestNode.EqualIgnoreCase(NodeName)) return;

        // 若本地已存在且哈希正确，则再次宣告新文件，复用扩散流程
        var exists = CheckLocalFile(req.AttachmentId, req.ExpectedHash);
        if (exists)
        {
            await PublishNewFileAsync(req.AttachmentId, CancellationToken.None).ConfigureAwait(false);
        }
    }
    #endregion

    #region 扫描同步
    /// <summary>批量扫描并请求缺失附件，返回已发出请求数。</summary>
    public async Task<Int32> ScanFilesAsync(Int32 batchSize = 50, CancellationToken cancellationToken = default)
    {
        var missing = GetMissingAttachmentIds(batchSize);
        var count = 0;
        foreach (var id in missing)
        {
            await RequestFileAsync(id, "sync missing", cancellationToken).ConfigureAwait(false);
            count++;
        }
        return count;
    }

    /// <summary>查询本地缺失的附件ID列表，用于触发同步。</summary>
    protected abstract Int64[] GetMissingAttachmentIds(Int32 batchSize);
    #endregion

    #region 辅助
    /// <summary>检查本地是否存在附件文件且哈希匹配。</summary>
    protected abstract Boolean CheckLocalFile(Int64 attachmentId, String? expectedHash);

    /// <summary>获取本地文件的元数据，用于消息携带（哈希、相对路径、MIME、大小）。</summary>
    protected abstract (String? hash, String? relativePath, String? contentType, Int64? length) GetLocalFileMeta(Int64 attachmentId);
    #endregion
}
