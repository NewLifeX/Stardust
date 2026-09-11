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
using Stardust.Services;
#if !NET45
using TaskEx = System.Threading.Tasks.Task;
#endif

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

    /// <summary>是否响应其他节点的文件下载请求。默认true</summary>
    public Boolean EnableProvide { get; set; } = true;

    /// <summary>是否主动拉取其他节点发布的新文件。默认true</summary>
    public Boolean EnableFetch { get; set; } = true;

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
    protected virtual Task OnInitializedAsync(CancellationToken cancellationToken)
    {
        // 统一事件总线工厂，自动选择通道（优先Redis队列、其次星尘通道），无需关心底层实现
        var factory = ServiceProvider?.GetService<IEventBusFactory>() ?? new StarEventBusFactory(ServiceProvider);
        NewFileBus = factory.CreateEventBus<NewFileInfo>(Name + "-NewFile");
        FileRequestBus = factory.CreateEventBus<FileRequest>(Name + "-FileRequest");

        return TaskEx.CompletedTask;
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

        if (NewFileBus is StarEventBus<FileRequest> bus && !bus.IsReady) return;

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
        // 附加消费上下文，便于排查问题（仅加标签，不改动消费逻辑）。
        // 【为什么用 DefaultSpan.Current，而不是下面 NewSpan 得到的 span】
        //   1) 下游 NewLife.Redis 消费循环中本回调执行时，RedisEventBus.ConsumeAsync 会先把 DefaultSpan.Current 置 null（打断调用链），
        //      再 new 一个 redismq:{topic}:Consume 子 span 压栈，因此本回调里 DefaultSpan.Current 实际指向 redismq:{topic}:Consume，
        //      并非 redis:RedisQueue:XREADGROUP-GROUP 命令 span（后者在 RedisClient.ExecuteAsync 内部 new、已自带命令参数 Tag）。
        //   2) 故本标签打在 redismq:{topic}:Consume 上，对该埋点自身排查有用；redis: 命令 span 由 Redis 客户端内部 new，
        //      本回调无法跨生命周期给它追加业务标签（且取消时无消息体可加），所以对 redis: 告警不带本标签。
        //   3) 下面的 Tracer.NewSpan("FileStorage-NewFile") 会新建一个星尘自建子 span，其标签同样只属于 FileStorage-NewFile 这条埋点。
        //   4) 时序：NewSpan 一执行就压栈成为新的 Current；本 AppendTag 必须在 NewSpan 之前取 DefaultSpan.Current，
        //      否则拿到的是刚建的 FileStorage-NewFile，而非 redismq:{topic}:Consume。
        //   DefaultSpan.Current 可能为 null（上游无 span），用 ?. 保证零侵入、不新增埋点。
        DefaultSpan.Current?.AppendTag(new { topic = "NewFile", consumer = Runtime.ClientId, sourceNode = info.SourceNode, nodeName = NodeName, fileId = info.Id, path = info.Path });

        var msg = info.ToJson();
        using var span = Tracer?.NewSpan("FileStorage-NewFile", msg);
        span?.Detach(info.TraceId);
        // 本 span 是星尘自建的子 span（FileStorage-NewFile），标签只属于该埋点，不会进入 redis: 告警
        span?.AppendTag(new { topic = "NewFile", sourceNode = info.SourceNode, nodeName = NodeName, fileId = info.Id, path = info.Path });

        // 默认忽略本节点自己发布的消息（除非需要自愈）
        if (info.SourceNode.EqualIgnoreCase(NodeName)) return;

        WriteLog("收到新文件通知：{0}", msg);

        // 检查本地是否已有文件且哈希正确。上传方可能还在处理（如向zip包注入配置），延迟重试几次再判定缺失
        if (!CheckLocalFile(info.Path, info.Hash))
        {
            for (var i = 0; i < 3 && !CheckLocalFile(info.Path, info.Hash); i++)
                await Task.Delay(200 * (i + 1), cancellationToken).ConfigureAwait(false);
        }
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

        // 若禁用拉取功能，仅记录地址信息，不拉取文件
        if (!EnableFetch)
        {
            //WriteLog("已禁用文件拉取功能，跳过：{0}", info.Name);
            return;
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

        if (FileRequestBus is StarEventBus<FileRequest> bus && !bus.IsReady) return;

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
        // 若禁用提供服务，则不响应文件请求
        if (!EnableProvide)
        {
            //WriteLog("已禁用文件提供服务，跳过文件请求响应：{0}", req.Name);
            return;
        }

        if (req.RequestNode.EqualIgnoreCase(NodeName)) return;

        WriteLog("收到请求文件通知：{0}", req.ToJson());

        // 附加消费上下文，便于排查问题（仅加标签，不改动消费逻辑）。
        // 【为什么用 DefaultSpan.Current，而不是下面 NewSpan 得到的 span】
        //   1) 下游 NewLife.Redis 消费循环中本回调执行时，RedisEventBus.ConsumeAsync 会先把 DefaultSpan.Current 置 null（打断调用链），
        //      再 new 一个 redismq:{topic}:Consume 子 span 压栈，因此本回调里 DefaultSpan.Current 实际指向 redismq:{topic}:Consume，
        //      并非 redis:RedisQueue:XREADGROUP-GROUP 命令 span（后者在 RedisClient.ExecuteAsync 内部 new、已自带命令参数 Tag）。
        //   2) 故本标签打在 redismq:{topic}:Consume 上，对该埋点自身排查有用；redis: 命令 span 由 Redis 客户端内部 new，
        //      本回调无法跨生命周期给它追加业务标签（且取消时无消息体可加），所以对 redis: 告警不带本标签。
        //   3) 下面的 Tracer.NewSpan("FileStorage-FileRequest") 会新建一个星尘自建子 span，其标签同样只属于 FileStorage-FileRequest 这条埋点。
        //   4) 时序：NewSpan 一执行就压栈成为新的 Current；本 AppendTag 必须在 NewSpan 之前取 DefaultSpan.Current，
        //      否则拿到的是刚建的 FileStorage-FileRequest，而非 redismq:{topic}:Consume。
        //   DefaultSpan.Current 可能为 null（上游无 span），用 ?. 保证零侵入、不新增埋点。
        DefaultSpan.Current?.AppendTag(new { topic = "FileRequest", consumer = Runtime.ClientId, requestNode = req.RequestNode, nodeName = NodeName, fileId = req.Id });

        using var span = Tracer?.NewSpan("FileStorage-FileRequest", new { req.Name, req.RequestNode });
        // 本 span 是星尘自建的子 span（FileStorage-FileRequest），标签只属于该埋点，不会进入 redis: 告警
        span?.AppendTag(new { topic = "FileRequest", requestNode = req.RequestNode, nodeName = NodeName, fileId = req.Id });

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
        if (FileRequestBus is StarEventBus<FileRequest> bus && !bus.IsReady) return -1;

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
        // 仅当本节点会拉取文件(Fetch=true)时才扫描缺失附件。
        // Fetch=false 的节点永不下载，扫描只会无限请求而无法被满足，造成消息队列暴涨。
        if (!EnableFetch) return;

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

        try
        {
            return fi.VerifyHash(hash);
        }
        catch (IOException)
        {
            // 文件可能正被写入或替换，按缺失处理，由上层延迟重试
            return false;
        }
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
