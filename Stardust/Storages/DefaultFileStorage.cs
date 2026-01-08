using System.Collections.Concurrent;
using System.Net;
using NewLife;
using NewLife.Caching;
using NewLife.Http;
using NewLife.Log;
using NewLife.Messaging;
using NewLife.Model;
using NewLife.Remoting;
using NewLife.Serialization;
using NewLife.Threading;

namespace Stardust.Storages;

/// <summary>分布式文件存储默认基类，用于编排文件同步流程。具体应用应继承并实现与存储相关的操作。</summary>
public abstract class DefaultFileStorage : DisposeBase, IFileStorage, ILogFeature, ITracerFeature
{
    #region 属性
    /// <summary>名称。作为事件总线Topic的前缀。相同名称的多个应用共享一个文件存储集群，如StarWeb和StarServer。默认为null时使用应用标识，集群部署的多节点应用实例共享文件存储</summary>
    public String? Name { get; set; }

    ///// <summary>用于广播地址消息的事件总线。</summary>
    //public IEventBus<AddressInfo>? AddressBus { get; set; }

    /// <summary>用于广播新文件消息的事件总线。</summary>
    public IEventBus<NewFileInfo>? NewFileBus { get; set; }

    /// <summary>用于发布文件请求消息的事件总线。</summary>
    public IEventBus<FileRequest>? FileRequestBus { get; set; }

    /// <summary>服务提供者，用于解析 StarFactory 等依赖。</summary>
    public IServiceProvider? ServiceProvider { get; set; }

    /// <summary>当前节点的逻辑名称（ClientId：IP@ProcessId）。</summary>
    public String? NodeName { get; set; } = Runtime.ClientId;

    /// <summary>作为文件存储的根路径</summary>
    public String? RootPath { get; set; }

    /// <summary>用于通过HTTP等方式拉取文件的基础地址</summary>
    public String DownloadUri { get; set; } = "/cube/file?id={Id}";

    private Int32 _initialized;
    private ICache _cache = new MemoryCache();

    // 地址缓存：节点名称 -> 地址信息
    private readonly ConcurrentDictionary<String, AddressInfo> _nodeAddresses = new(StringComparer.OrdinalIgnoreCase);

    private TimerX? _scanTimer;
    #endregion

    #region 构造
    /// <summary>销毁释放</summary>
    /// <param name="disposing"></param>
    protected override void Dispose(Boolean disposing)
    {
        base.Dispose(disposing);

        NewFileBus.TryDispose();
        FileRequestBus.TryDispose();
        _scanTimer?.Dispose();
    }
    #endregion

    #region 初始化
    /// <summary>初始化，确保订阅事件总线并设置处理逻辑。</summary>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref _initialized, 1) == 1) return;

        WriteLog("[{0}]初始化分布式文件存储，节点：{1}", Name, NodeName);
        using var span = Tracer?.NewSpan("FileStorage-Init", new { Name, NodeName });

        await OnInitializedAsync(cancellationToken).ConfigureAwait(false);

        // 订阅新文件、文件请求消息
        NewFileBus?.Subscribe(OnNewFileInfoAsync);
        FileRequestBus?.Subscribe(OnFileRequestAsync);

        _scanTimer = new TimerX(OnScan, null, 10_000, 3600_000) { Async = true };
    }

    /// <summary>事件总线订阅完成后调用，覆写以执行额外初始化。</summary>
    protected virtual Task OnInitializedAsync(CancellationToken cancellationToken) => Task.FromResult(0);

    /// <summary>设置事件总线</summary>
    /// <param name="cacheProvider"></param>
    /// <returns></returns>
    public Boolean SetEventBus(ICacheProvider cacheProvider)
    {
        if (cacheProvider.Cache is not Cache cache) return false;

        WriteLog("使用[{0}]事件总线，订阅[{1}]的应用通过消息队列分发事件。", cache.GetType().Name, Name);

        var clientId = Runtime.ClientId;
        NewFileBus = cache.CreateEventBus<NewFileInfo>(Name + "-NewFile", clientId);
        FileRequestBus = cache.CreateEventBus<FileRequest>(Name + "-FileRequest", clientId);

        return true;
    }

    /// <summary>设置事件总线</summary>
    /// <param name="client"></param>
    /// <returns></returns>
    public Boolean SetEventBus(AppClient client)
    {
        if (client == null) return false;

        NewFileBus = client.GetEventBus<NewFileInfo>(Name + "-NewFile");
        FileRequestBus = client.GetEventBus<FileRequest>(Name + "-FileRequest");

        return true;
    }
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

        var inf = BuildAddressInfo();
        msg.InternalAddress = inf.InternalAddress;
        msg.ExternalAddress = inf.ExternalAddress;

        await NewFileBus.PublishAsync(msg, null, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>处理新文件消息。</summary>
    protected virtual async Task OnNewFileInfoAsync(NewFileInfo info, IEventContext context, CancellationToken cancellationToken)
    {
        var msg = info.ToJson();
        using var span = Tracer?.NewSpan("FileStorage-NewFile", msg);
        span?.Detach(info.TraceId);

        // 默认忽略本节点自己发布的消息（除非需要自愈）
        if (info.SourceNode.EqualIgnoreCase(NodeName)) return;

        WriteLog("收到新文件通知：{0}", msg);

        // 检查本地是否已有文件且哈希正确
        if (CheckLocalFile(info.Path, info.Hash)) return;

        var node = info.SourceNode + "";
        if (!info.InternalAddress.IsNullOrEmpty() && !info.ExternalAddress.IsNullOrEmpty())
        {
            _nodeAddresses[node] = new AddressInfo
            {
                NodeName = info.SourceNode,
                InternalAddress = info.InternalAddress,
                ExternalAddress = info.ExternalAddress
            };
        }

        try
        {
            // 从源节点拉取文件数据
            await FetchFileAsync(info, node, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            span?.SetError(ex, null);
        }
    }

    /// <summary>通过应用自定义的传输方式（如HTTP接口）从指定源节点拉取文件数据。</summary>
    protected virtual async Task FetchFileAsync(IFileInfo file, String sourceNode, CancellationToken cancellationToken)
    {
        if (file == null || file.Path.IsNullOrEmpty()) throw new ArgumentNullException(nameof(file));
        if (sourceNode.IsNullOrEmpty()) throw new ArgumentNullException(nameof(sourceNode));

        if (!_nodeAddresses.TryGetValue(sourceNode, out var addr))
            throw new InvalidDataException($"无法解析节点[{sourceNode}]的地址");

        var url = DownloadUri;
        url = url.Replace("{Id}", file.Id + "");
        url = url.Replace("{Name}", file.Name);

        using var span = Tracer?.NewSpan("FileStorage-FetchFile", new { file.Name, url });
        WriteLog("下载文件：{0}，来源：{1}", file.Name, url);
        try
        {
            var fileName = RootPath.CombinePath(file.Path).GetFullPath();

            // 获取源节点基地址，通过HTTP等方式拉取文件数据
            var key = $"client:{addr.NodeName}:{addr.InternalAddress}:{addr.ExternalAddress}";
            var client = _cache.Get<ApiHttpClient>(key);
            if (client == null)
            {
                client = new ApiHttpClient
                {
                    LoadBalanceMode = LoadBalanceMode.Race,
                    DefaultUserAgent = HttpHelper.DefaultUserAgent,
                    Timeout = 3_000,
                    Tracer = Tracer,
                    Log = Log,
                };
                if (!addr.InternalAddress.IsNullOrEmpty()) client.AddServer("内网", addr.InternalAddress);
                if (!addr.ExternalAddress.IsNullOrEmpty()) client.AddServer("外网", addr.ExternalAddress);

                _cache.Set(key, client, 600);
            }

            //await client.DownloadFileAsync(url, fileName, file.Hash, cancellationToken).ConfigureAwait(false);
            // 使用竞速下载
            await client.DownloadFileRaceAsync(url, fileName, file.Hash, false, cancellationToken).ConfigureAwait(false);

            WriteLog("下载文件：{0}，成功：{1}", file.Name, client.Current?.Address);
        }
        catch (Exception ex)
        {
            span?.SetError(ex);
            WriteLog("下载文件：{0}，异常：{1}", file.Name, ex.Message);
        }
    }
    #endregion

    #region 文件请求
    /// <summary>发布请求，向其他节点索取指定附件。</summary>
    public Task RequestFileAsync(Int64 attachmentId, String? path, String? reason = null, CancellationToken cancellationToken = default)
    {
        if (FileRequestBus == null) throw new InvalidOperationException("FileRequestBus not configured.");

        var file = GetLocalFileMeta(attachmentId, path);
        return RequestFileAsync(file, reason, cancellationToken);
    }

    /// <summary>发布请求，向其他节点索取指定附件。</summary>
    public async Task RequestFileAsync(IFileInfo file, String? reason = null, CancellationToken cancellationToken = default)
    {
        if (FileRequestBus == null) throw new InvalidOperationException("FileRequestBus not configured.");

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
    protected virtual async Task OnFileRequestAsync(FileRequest req, IEventContext context, CancellationToken cancellationToken)
    {
        if (req.RequestNode.EqualIgnoreCase(NodeName)) return;

        WriteLog("收到请求文件通知：{0}", req.ToJson());

        using var span = Tracer?.NewSpan("FileStorage-FileRequest", new { req.Name, req.RequestNode });

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
    public virtual async Task<Int32> ScanFilesAsync(DateTime startTime, CancellationToken cancellationToken = default)
    {
        using var span = Tracer?.NewSpan("FileStorage-ScanFiles", new { startTime });

        var count = 0;
        foreach (var file in GetMissingAttachments(startTime))
        {
            await RequestFileAsync(file.Id, file.Path, "sync missing", cancellationToken).ConfigureAwait(false);
            count++;
            if (span != null) span.Value++;
        }

        return count;
    }

    private DateTime _last;
    private async Task OnScan(Object state)
    {
        //if (_last.Year < 2000) _last = DateTime.Today.AddDays(-30);

        // 执行扫描，如果成功更新下一次执行时间，如果失败则快速重试
        var rs = await ScanFilesAsync(_last, default).ConfigureAwait(false);
        if (rs >= 0)
            _last = DateTime.Now;
        else
            _scanTimer?.SetNext(10_000);
    }

    /// <summary>查询从指定时间开始本地缺失的附件，用于触发同步。</summary>
    protected virtual IEnumerable<IFileInfo> GetMissingAttachments(DateTime startTime)
    {
        yield break;
    }
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

    /// <summary>构建当前节点地址信息。</summary>
    protected virtual AddressInfo BuildAddressInfo()
    {
        String? internalAddr = null;
        String? externalAddr = null;

        // 从 ServiceProvider 解析 StarFactory，尽量获取更准确的内外网地址
        var factory = ServiceProvider?.GetService<StarFactory>();
        if (factory != null)
        {
            internalAddr = factory.InternalAddress;
            externalAddr = factory.ExternalAddress;
        }

        // 降级：使用 StarSetting 或已有 ServiceAddress
        //if (internalAddr.IsNullOrEmpty()) internalAddr = StarSetting.Current.ServiceAddress;
        if (externalAddr.IsNullOrEmpty()) externalAddr = StarSetting.Current.ServiceAddress;

        // 展开内网地址
        if (!internalAddr.IsNullOrEmpty())
        {
            var urls = new List<String>();
            foreach (var ip in NetHelper.GetIPsWithCache())
            {
                if (IPAddress.IsLoopback(ip)) continue;
                var buf = ip.GetAddressBytes();
                if (buf[0] == 169 && buf[1] == 254) continue;
                if (buf[0] == 0xfe && buf[1] == 0x80) continue;

                var ip2 = ip.IsIPv4() ? ip.ToString() : $"[{ip}]";
                var addrs = internalAddr
                    .Replace("://*", $"://{ip2}")
                    .Replace("://+", $"://{ip2}")
                    .Replace("://0.0.0.0", $"://{ip2}")
                    .Replace("://[::]", $"://{ip2}")
                    .Split(",");

                foreach (var elm in addrs)
                {
                    var url = elm;
                    if (url.StartsWithIgnoreCase("http://", "https://"))
                        url = new Uri(url).ToString().TrimEnd('/');
                    if (!urls.Contains(url)) urls.Add(url);
                }
            }
            internalAddr = urls.Join(",");
        }

        var info = new AddressInfo
        {
            NodeName = NodeName,
            InternalAddress = internalAddr,
            ExternalAddress = externalAddr,
        };

        //// 更新本地缓存，便于后续下载使用
        //_nodeAddresses[info.NodeName + ""] = info;

        return info;
    }
    #endregion

    #region 日志
    /// <summary>追踪器</summary>
    public ITracer? Tracer { get; set; }

    /// <summary>日志</summary>
    public ILog Log { get; set; } = Logger.Null;

    /// <summary>写日志</summary>
    public void WriteLog(String format, params Object?[] args) => Log?.Info(format, args);
    #endregion
}
