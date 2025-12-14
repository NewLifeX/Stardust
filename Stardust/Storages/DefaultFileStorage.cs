using NewLife;
using NewLife.Http;
using NewLife.Log;
using NewLife.Messaging;
using NewLife.Serialization;

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
    public String? NodeName { get; set; } = Environment.MachineName;

    /// <summary>作为文件存储的根路径</summary>
    public String? RootPath { get; set; }

    /// <summary>用于通过HTTP等方式拉取文件的基础地址</summary>
    public String DownloadUri { get; set; } = "/cube/file?id={Id}";

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
    public Task PublishNewFileAsync(Int64 attachmentId, String? path, CancellationToken cancellationToken = default)
    {
        if (NewFileBus == null) throw new InvalidOperationException("NewFileBus not configured.");

        var file = GetLocalFileMeta(attachmentId, path);
        return PublishNewFileAsync(file, cancellationToken);
    }

    /// <summary>广播指定附件在当前节点可用。</summary>
    public async Task PublishNewFileAsync(IFileInfo file, CancellationToken cancellationToken = default)
    {
        if (NewFileBus == null) throw new InvalidOperationException("NewFileBus not configured.");

        if (file is not NewFileInfo msg)
        {
            msg = new NewFileInfo
            {
                Id = file.Id,
                Name = file.Name,
                Path = file.Path,
                Hash = file.Hash,
                Length = file.Length,
            };
        }
        msg.SourceNode = NodeName;

        // 填充节点地址，供其他节点拉取文件
        if (msg.NodeAddress.IsNullOrEmpty())
        {
            //todo: 区分内网地址和外网地址
            msg.NodeAddress = Setting.Current.ServiceAddress;
        }

        await NewFileBus.PublishAsync(msg, null, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>处理新文件消息。</summary>
    protected virtual async Task OnNewFileInfoAsync(NewFileInfo info, IEventContext<NewFileInfo> context, CancellationToken cancellationToken)
    {
        XTrace.WriteLine("新文件通知：{0}", info.ToJson());

        // 默认忽略本节点自己发布的消息（除非需要自愈）
        if (info.SourceNode.EqualIgnoreCase(NodeName)) return;

        // 检查本地是否已有文件且哈希正确
        if (CheckLocalFile(info.Path, info.Hash)) return;

        // 从源节点拉取文件数据
        await FetchFileAsync(info, info.SourceNode, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>通过应用自定义的传输方式（如HTTP接口）从指定源节点拉取文件数据。</summary>
    protected virtual async Task FetchFileAsync(IFileInfo file, String? sourceNode, CancellationToken cancellationToken)
    {
        if (file == null || file.Path.IsNullOrEmpty()) throw new ArgumentNullException(nameof(file));

        var url = DownloadUri;
        url = url.Replace("{Id}", file.Id + "");
        url = url.Replace("{Name}", file.Name);

        var fileName = RootPath.CombinePath(file.Path).GetFullPath();

        //todo: 获取源节点基地址，通过HTTP等方式拉取文件数据
        var client = new HttpClient();
        if (file is NewFileInfo fileInfo && !fileInfo.NodeAddress.IsNullOrEmpty())
            client.BaseAddress = new Uri(fileInfo.NodeAddress.Split(";")[0]);

        await client.DownloadFileAsync(url, fileName, file.Hash, cancellationToken).ConfigureAwait(false);
    }
    #endregion

    #region 文件请求
    /// <summary>发布请求，向其他节点索取指定附件。</summary>
    public async Task RequestFileAsync(Int64 attachmentId, String? path, String? reason = null, CancellationToken cancellationToken = default)
    {
        if (FileRequestBus == null) throw new InvalidOperationException("FileRequestBus not configured.");

        var file = GetLocalFileMeta(attachmentId, path);
        var msg = new FileRequest
        {
            Id = file.Id,
            Name = file.Name,
            Path = file.Path,
            Hash = file.Hash,
            Reason = reason,
            RequestNode = NodeName,
        };
        await FileRequestBus.PublishAsync(msg, null, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>处理文件请求消息。</summary>
    protected virtual async Task OnFileRequestAsync(FileRequest req, IEventContext<FileRequest> context, CancellationToken cancellationToken)
    {
        XTrace.WriteLine("请求文件通知：{0}", req.ToJson());

        if (req.RequestNode.EqualIgnoreCase(NodeName)) return;

        // 若本地已存在且哈希正确，则再次宣告新文件，复用扩散流程
        var exists = CheckLocalFile(req.Path, req.Hash);
        if (exists)
        {
            await PublishNewFileAsync(req.Id, req.Path, cancellationToken).ConfigureAwait(false);
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
            await RequestFileAsync(id, null, "sync missing", cancellationToken).ConfigureAwait(false);
            count++;
        }
        return count;
    }

    /// <summary>查询本地缺失的附件ID列表，用于触发同步。</summary>
    protected abstract Int64[] GetMissingAttachmentIds(Int32 batchSize);
    #endregion

    #region 辅助
    /// <summary>检查本地是否存在附件文件且哈希匹配。</summary>
    protected virtual Boolean CheckLocalFile(String? path, String? hash)
    {
        if (path.IsNullOrEmpty()) return false;

        var fi = RootPath.CombinePath(path).AsFile();
        if (!fi.Exists) return false;

        if (hash.IsNullOrEmpty()) return true;

        return fi.VerifyHash(hash);
    }

    /// <summary>获取本地文件的元数据</summary>
    protected virtual IFileInfo GetLocalFileMeta(Int64 attachmentId, String? path)
    {
        if (path.IsNullOrEmpty()) throw new ArgumentNullException(nameof(path));

        var fi = RootPath.CombinePath(path).AsFile();

        return new NewFileInfo
        {
            Id = attachmentId,
            Name = fi.Name,
            Path = path,
            Hash = fi.MD5().ToHex(),
            Length = fi.Length,
        };
    }
    #endregion
}
