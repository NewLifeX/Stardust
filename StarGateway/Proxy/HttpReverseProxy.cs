using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NewLife;
using NewLife.Collections;
using NewLife.Data;
using NewLife.Http;
using NewLife.Log;
using NewLife.Net;
using NewLife.Remoting;
using NewLife.Serialization;
using NewLife.Threading;
using Stardust;
using Stardust.Models;
using Stardust.Data.Deployment;
using Stardust.Data.Gateway;

namespace StarGateway.Proxy;

/// <summary>Http反向代理。支持动态路由配置，从数据库加载路由规则</summary>
public class HttpReverseProxy : ProxyServer
{
    #region 属性
    /// <summary>远程服务器地址（默认兜底）</summary>
    public NetUri RemoteServer { get; set; } = new();

    /// <summary>路由缓存快照</summary>
    private volatile IList<GatewayRoute> _routes;

    /// <summary>本地兜底直连目标表。键为本地文件生成的负 Id，值为直连后端地址（ClusterId=0 时由 SelectNode 直接转发，不经数据库节点查询）</summary>
    private readonly ConcurrentDictionary<Int32, String> _directTargets = new();

    /// <summary>标记本次配置是否来自 StarServer（远程优先覆盖）。用于在远程成功时跳过数据库证书加载，实现证书同源覆盖+回退查库。用 Interlocked 原子读写避免读改写竞态</summary>
    private Int32 _configFromServer;

    /// <summary>当前配置来源（server/database/file/none），用于运维观测</summary>
    private String _configSource = "none";

    /// <summary>非安全(http) StarServer 已告警标记，避免每次刷新重复刷日志</summary>
    private static Boolean _warnedInsecureServer;

    /// <summary>配置刷新间隔。默认15秒</summary>
    public Int32 ConfigRefreshInterval { get; set; } = 15;

    /// <summary>健康检查间隔。默认10秒</summary>
    public Int32 HealthCheckInterval { get; set; } = 10;

    private TimerX _timer;
    private TimerX _healthTimer;

    /// <summary>总请求数</summary>
    internal Int64 _totalRequests;

    /// <summary>连接计数（用于最少连接负载均衡）</summary>
    internal ConcurrentDictionary<String, Int32> _connectionCounts = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>管理员日志</summary>
    public ILog AdminLog { get; set; }

    /// <summary>静态文件处理器</summary>
    public StaticFileHandler StaticFiles { get; set; } = new();
    #endregion

    #region 构造
    public HttpReverseProxy()
    {
        Name = "Gateway";
        Port = 8080;
        ProtocolType = NetType.Tcp;
    }
    #endregion

    #region 启动停止
    protected override void OnStart()
    {
        var set = StarGatewaySetting.Current;

        if (set.ConfigRefreshInterval > 0) ConfigRefreshInterval = set.ConfigRefreshInterval;
        if (set.HealthCheckInterval > 0) HealthCheckInterval = set.HealthCheckInterval;
        if (set.IdleTimeout > 0) IdleTimeout = set.IdleTimeout;

        // 先尝试从StarServer加载，失败则从数据库加载，再失败则从本地文件加载
        LoadConfigWithFallbackAsync().Wait();

        // 加载SSL证书（远程已覆盖则跳过，回退查库）
        if (Interlocked.CompareExchange(ref _configFromServer, 0, 0) == 0) LoadCertificates();

        // 初始化静态文件处理器
        {
            var sfh = StaticFiles;
            sfh.Log = AdminLog;
            sfh.LogEnabled = set.Debug;
        }

        _timer = new TimerX(DoRefreshConfig, null, ConfigRefreshInterval * 1000, ConfigRefreshInterval * 1000) { Async = true };
        _healthTimer = new TimerX(DoHealthCheck, null, HealthCheckInterval * 1000, HealthCheckInterval * 1000) { Async = true };

        WriteLog("Http反向代理启动，监听端口：{0}，路由数：{1}", Port, _routes?.Count ?? 0);
        base.OnStart();
    }
    #endregion

    #region SSL证书加载
    protected virtual void LoadCertificates()
    {
        try
        {
            // 数据库共享模式：证书来自 SslCertificate 表（StarServer 后台写入的同一库）
            var certs = SslCertificate.FindAllEnabled().Select(e => new GatewayCertInfo
            {
                Domain = e.Domain,
                CertFile = e.PemFile ?? e.CrtFile ?? e.PfxFile,
                KeyFile = e.KeyFile,
                PfxPassword = e.PfxPassword,
            }).ToList();
            ApplyCerts(certs);
        }
        catch (Exception ex)
        {
            WriteError("加载SSL证书配置失败：{0}", ex.Message);
        }
    }

    /// <summary>将证书列表（来自 StarServer 或数据库）应用到当前代理，加载第一个可用证书（SNI多证书支持在后续版本完善）。</summary>
    protected virtual void ApplyCerts(IList<GatewayCertInfo> certs)
    {
        if (certs == null || certs.Count == 0)
        {
            WriteLog("未配置SSL证书，仅支持HTTP");
            return;
        }

        foreach (var cert in certs)
        {
            var file = cert.CertFile;
            if (file.IsNullOrEmpty() || !File.Exists(file)) continue;

            try
            {
#pragma warning disable SYSLIB0057
                X509Certificate2 x509;
                if (file.EndsWith(".pfx", StringComparison.OrdinalIgnoreCase))
                {
                    // PFX 可能带密码
                    x509 = !cert.PfxPassword.IsNullOrEmpty()
                        ? new X509Certificate2(file, cert.PfxPassword)
                        : new X509Certificate2(file);
                }
                else
                {
                    // PEM/CRT：支持独立私钥文件（KeyFile）；未提供私钥文件时直接加载（兼容证书+私钥合并的单文件）
                    var keyFile = cert.KeyFile;
                    x509 = !keyFile.IsNullOrEmpty() && File.Exists(keyFile)
                        ? X509Certificate2.CreateFromPemFile(file, keyFile)
                        : new X509Certificate2(file);
                }
#pragma warning restore SYSLIB0057
                Certificate = x509;
                SslProtocol = SslProtocols.Tls12;
                WriteLog("加载SSL证书: {0} -> {1}", cert.Domain, x509.Subject);
                break;
            }
            catch (Exception ex)
            {
                WriteError("加载证书 {0} 失败：{1}", file, ex.Message);
            }
        }
    }
    #endregion

    #region 配置加载（多级兜底）
    /// <summary>多级加载网关路由配置，优先级从高到低：远程 StarServer > 本地数据库 > 本地配置文件。
    /// 采用“首选胜出”覆盖策略（非合并）：远程成功即返回，失败或不可用才回退后续来源。</summary>
    protected virtual async Task LoadConfigWithFallbackAsync()
    {
        // 默认按数据库/本地兜底加载证书；仅当远程成功且确实下发证书时，
        // 由 LoadConfigFromServerAsync 置位 _configFromServer，跳过数据库证书加载
        Interlocked.Exchange(ref _configFromServer, 0);

        // 1. 从 StarServer 拉取配置（最高优先级，覆盖后续来源）
        try
        {
            if (!StarGatewaySetting.Current.StarServer.IsNullOrEmpty()
                && await LoadConfigFromServerAsync()) return;
        }
        catch (Exception ex)
        {
            WriteError("从StarServer加载配置失败：{0}", ex.Message);
        }

        // 2. 从数据库加载（StarServer 后台写入的同一数据库，作为主要兜底源）
        try
        {
            LoadConfig();
            if (_routes != null && _routes.Count > 0)
            {
                WriteLog("从数据库加载路由配置完成，共 {0} 条路由", _routes.Count);
                return;
            }
        }
        catch (Exception ex)
        {
            WriteError("从数据库加载配置失败：{0}", ex.Message);
        }

        // 3. 从本地配置文件兜底
        try
        {
            LoadConfigFromLocalFile();
        }
        catch (Exception ex)
        {
            WriteError("从本地文件加载配置失败：{0}", ex.Message);
        }
    }

    /// <summary>从 StarServer 拉取网关路由配置。成功返回 true 并已更新 _routes；
    /// 未配置 StarServer 或拉取失败返回 false，由上层回退数据库。</summary>
    protected virtual async Task<Boolean> LoadConfigFromServerAsync()
    {
        // 复用 StarFactory 的 StarServer 客户端；未配置 StarServer 时 Client 为 null
        var client = Program.Star?.Client;
        if (client == null) return false;

        // P1-4：设置调用超时，避免 StarServer 不可达时无限等待
        if (client is ApiClient ac) ac.Timeout = 10_000;

        try
        {
            // P1-3：以网关自身应用身份（StarAppId/StarSecret/ClientId）调用，
            // 与服务端 [ApiFilter]+Valid 的应用级鉴权对应；token 留空走密钥校验
            var cfg = await client.InvokeAsync<GatewayConfig>("Gateway/config", new
            {
                appId = Program.Star?.AppId,
                secret = Program.Star?.Secret,
                clientId = Program.Star?.ClientId,
                token = "",
            });

            // 将服务端下发的路由映射为内存路由表：仅取标量字段 + ClusterId，
            // 节点仍由现有转发/健康检查逻辑按 ClusterId 从数据库查询（符合数据库共享模式设计）
            var routes = new List<GatewayRoute>();
            foreach (var r in cfg.Routes)
            {
                // 与数据库 FindAllEnabled 行为一致：跳过禁用路由
                if (!r.Enable) continue;

                routes.Add(new GatewayRoute
                {
                    Id = r.Id,
                    Name = r.Name,
                    Priority = r.Priority,
                    Domain = r.Domain,
                    Path = r.Path,
                    Methods = r.Methods,
                    Headers = r.Headers,
                    StripPrefix = r.StripPrefix,
                    AddHeaders = r.AddHeaders,
                    WebSocket = r.WebSocket,
                    IsStaticRoute = r.IsStaticRoute,
                    StaticRoot = r.StaticRoot,
                    IndexFile = r.IndexFile,
                    DirectoryBrowse = r.DirectoryBrowse,
                    SPAFallback = r.SPAFallback,
                    ClusterId = r.Cluster?.Id ?? 0,
                });
            }

            // P1-1：远程路由为空时视为未提供有效配置，直接回退数据库（不覆盖、不阻断）
            if (routes.Count == 0)
            {
                WriteLog("从 StarServer 拉取的路由为空，回退数据库");
                return false;
            }

            Interlocked.Exchange(ref _routes, routes);
            _configSource = "server";
            WriteLog("从 StarServer 拉取路由配置完成，共 {0} 条路由", routes.Count);

            // 证书同源覆盖：仅当远程确实下发证书时才覆盖并跳过数据库证书加载；
            // 若远程证书列表为空，_configFromServer 保持 0（未覆盖），由上层回退查库
            if (cfg.Certs != null && cfg.Certs.Count > 0)
            {
                // P1：StarServer 为明文 http 时，证书私钥密码会在链路上暴露，
                // 故拒绝从远程加载证书（回退数据库证书加载），仅放行路由配置，并仅告警一次
                var server = Program.Star?.Server;
                if (server.IsNullOrEmpty() || !server.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    if (!_warnedInsecureServer)
                    {
                        _warnedInsecureServer = true;
                        WriteError("StarServer 使用明文 http，出于安全考虑拒绝从远程加载证书（私钥密码会在链路上暴露），证书回退数据库加载；生产环境请改用 https");
                    }
                }
                else
                {
                    ApplyCerts(cfg.Certs);
                    Interlocked.Exchange(ref _configFromServer, 1);
                }
            }
            return true;
        }
        catch (Exception ex)
        {
            WriteError("从 StarServer 拉取配置失败，回退数据库：{0}", ex.Message);
            return false;
        }
    }

    protected virtual void LoadConfigFromLocalFile()
    {
        var file = StarGatewaySetting.Current.LocalConfigFile;
        if (file.IsNullOrEmpty() || !File.Exists(file)) return;

        var json = File.ReadAllText(file);
        if (json.IsNullOrEmpty()) return;

        // 解析本地兜底配置文件（Server 与 DB 均不可达时的最终兜底）
        // 格式: [{ "name":"route1", "domain":"*.example.com", "path":"/api/*", "methods":"GET", "target":"http://localhost:5000" }]
        // 采用直连 target（ClusterId=0），由 SelectNode 直接转发，不经数据库节点查询
        var list = json.ToJsonEntity<IList<IDictionary<String, Object?>>>();
        if (list == null || list.Count == 0) return;

        var routes = new List<GatewayRoute>();
        var targets = new Dictionary<Int32, String>();
        var idx = 0;
        foreach (var item in list)
        {
#pragma warning disable CS8632 // 只能在 "#nullable" 注释上下文内的代码中使用可为 null 的引用类型的注释。
            if (item is not IDictionary<String, Object?> raw) continue;
#pragma warning restore CS8632 // 只能在 "#nullable" 注释上下文内的代码中使用可为 null 的引用类型的注释。
            // 键名大小写不敏感，允许用户在 JSON 中写 Name 或 name
#pragma warning disable CS8632 // 只能在 "#nullable" 注释上下文内的代码中使用可为 null 的引用类型的注释。
            var dic = new NullableDictionary<String, Object?>(raw, StringComparer.OrdinalIgnoreCase);
#pragma warning restore CS8632 // 只能在 "#nullable" 注释上下文内的代码中使用可为 null 的引用类型的注释。

            var name = dic["name"] + "";
            var domain = dic["domain"] + "";
            var target = dic["target"] + "";
            // 静态文件路由：有 staticRoot 即视为静态路由，此时不要求 target
            var staticRoot = dic["staticRoot"] + "";
            var isStatic = !staticRoot.IsNullOrEmpty();
            // 名称/域名为必填；既非静态又无 target 的路由无意义，跳过
            if (name.IsNullOrEmpty() || domain.IsNullOrEmpty()) continue;
            if (target.IsNullOrEmpty() && !isStatic) continue;

            // 负 Id 避免与数据库路由 Id 冲突
            var id = -(++idx);
            routes.Add(new GatewayRoute
            {
                Id = id,
                Name = name,
                Domain = domain,
                Path = dic["path"] + "",
                Methods = dic["methods"] + "",
                Priority = (dic["priority"] + "").ToInt(),
                Enable = true,
                ClusterId = 0,
                IsStaticRoute = isStatic,
                StaticRoot = staticRoot,
                IndexFile = dic["indexFile"] + "",
                DirectoryBrowse = (dic["directoryBrowse"] + "").ToBoolean(),
                SPAFallback = (dic["spaFallback"] + "").ToBoolean(),
            });
            // 仅反向代理路由需要直连目标；静态路由交由静态文件分支处理
            if (!target.IsNullOrEmpty()) targets[id] = target;
        }

        if (routes.Count == 0) return;

        // 原子替换直连目标表与路由快照
        _directTargets.Clear();
        foreach (var kv in targets) _directTargets[kv.Key] = kv.Value;
        Interlocked.Exchange(ref _routes, routes);
        _configSource = "file";
        WriteLog("从本地文件 {0} 加载路由配置，共 {1} 条（本地兜底）", file, routes.Count);
    }

    protected virtual void LoadConfig()
    {
        try
        {
            var routes = GatewayRoute.FindAllEnabled();
            Interlocked.Exchange(ref _routes, routes);
            _configSource = "database";
        }
        catch (Exception ex)
        {
            WriteError("加载路由配置失败：{0}", ex.Message);
        }
    }

    private async Task DoRefreshConfig(Object state)
    {
        await LoadConfigWithFallbackAsync();

        // 配置刷新时同时刷新证书（证书热更新）；远程已覆盖则跳过，回退查库
        if (Interlocked.CompareExchange(ref _configFromServer, 0, 0) == 0) LoadCertificates();

        await Task.CompletedTask;
    }

    public void RefreshConfig() => LoadConfigWithFallbackAsync().GetAwaiter().GetResult();
    #endregion

    #region 健康检查
    private async Task DoHealthCheck(Object state)
    {
        var routes = _routes;
        if (routes == null || routes.Count == 0) return;

        foreach (var route in routes)
        {
            var nodes = GatewayNode.FindAllByClusterId(route.ClusterId);
            if (nodes == null) continue;

            foreach (var node in nodes)
            {
                if (!node.Enable) continue;

                // TCP端口探测
                var healthy = await ProbeAddress(node.Address);
                if (node.IsHealthy != healthy)
                {
                    node.IsHealthy = healthy;
                    node.Update();
                    WriteLog("健康检查: {0} -> {1}", node.Address, healthy ? "🟢" : "🔴");
                }
            }
        }
    }

    private static async Task<Boolean> ProbeAddress(String address)
    {
        if (address.IsNullOrEmpty()) return false;

        try
        {
            // 解析地址
            var uri = new NetUri(address);
            using var tcp = new TcpClient();

            // 异步连接，避免同步阻塞导致线程池饥饿
            var connectTask = tcp.ConnectAsync(uri.Address, uri.Port);
            var timeoutTask = Task.Delay(3000);
            var completed = await Task.WhenAny(connectTask, timeoutTask);

            if (completed == connectTask && tcp.Connected)
                return true;

            return false;
        }
        catch
        {
            return false;
        }
    }
    #endregion

    #region 会话管理
    protected override INetSession CreateSession(ISocketSession session)
    {
        var rs = new HttpReverseSession { Host = this };
        return rs;
    }
    #endregion

    #region 路由匹配
    public GatewayRoute MatchRoute(String domain, String path, String method, IDictionary<String, String> headers = null)
    {
        var routes = _routes;
        if (routes == null || routes.Count == 0) return null;

        foreach (var route in routes)
        {
            if (route.Match(domain, path, method, headers)) return route;
        }
        return null;
    }

    public NetUri SelectNode(GatewayRoute route, String clientIp = null)
    {
        // 本地兜底：ClusterId=0。静态路由交由静态文件分支处理，不在此选节点；
        // 反向代理路由按直连 target 返回，不经数据库节点查询
        if (route.ClusterId == 0)
        {
            if (route.IsStaticRoute) return null;
            if (_directTargets.TryGetValue(route.Id, out var target) && !target.IsNullOrEmpty())
                return new NetUri(target);
            return null;
        }

        var nodes = GatewayNode.FindAllHealthyByCluster(route.ClusterId);
        if (nodes == null || nodes.Count == 0) return null;

        var cluster = route.Cluster;
        var lb = cluster?.LoadBalance ?? "RoundRobin";

        GatewayNode selected;
        switch (lb)
        {
            case "LeastConnection":
                selected = SelectLeastConnection(nodes);
                break;
            case "IPHash":
                selected = SelectIPHash(nodes, clientIp ?? "");
                break;
            case "RoundRobin":
            default:
                selected = SelectRoundRobin(nodes);
                break;
        }

        if (selected == null || selected.Address.IsNullOrEmpty()) return null;
        return new NetUri(selected.Address);
    }

    private static GatewayNode SelectRoundRobin(IList<GatewayNode> nodes)
    {
        var index = Environment.TickCount % nodes.Count;
        return nodes[index >= 0 ? index : 0];
    }

    private GatewayNode SelectLeastConnection(IList<GatewayNode> nodes)
    {
        // 真正的最少连接：找到当前活跃连接数最少的节点
        var min = Int32.MaxValue;
        GatewayNode selected = null;
        foreach (var node in nodes)
        {
            var key = node.Address;
            var count = _connectionCounts.GetOrAdd(key, _ => 0);
            if (count < min)
            {
                min = count;
                selected = node;
            }
        }
        return selected ?? nodes[0];
    }

    private static GatewayNode SelectIPHash(IList<GatewayNode> nodes, String ip)
    {
        var hash = ip.IsNullOrEmpty() ? 0 : ip.GetHashCode();
        var index = Math.Abs(hash) % nodes.Count;
        return nodes[index];
    }

    internal void IncrementConnection(String address)
    {
        _connectionCounts.AddOrUpdate(address, 1, (_, v) => v + 1);
    }

    internal void DecrementConnection(String address)
    {
        _connectionCounts.AddOrUpdate(address, 0, (_, v) => Math.Max(0, v - 1));
    }
    #endregion

    #region Admin API
    public Boolean HandleAdminRequest(HttpReverseSession session, String path, HttpRequest request)
    {
        if (!path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase)) return false;

        // P1-3：Admin API 鉴权。回环地址默认可访问（本机运维工具）；非回环需配置 AdminToken 且请求携带匹配令牌
        if (!IsAdminAuthorized(session, request))
        {
            var body = "Admin API 需要鉴权"u8.ToArray();
            session.Send($"HTTP/1.1 401 Unauthorized\r\nWWW-Authenticate: Basic realm=\"Stardust网关：用户名随意，密码填AdminToken\"\r\nContent-Type: text/plain; charset=utf-8\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n");
            session.Send(body);
            session.Dispose();
            return true;
        }

        Interlocked.Increment(ref _totalRequests);

        var json = "";
        if (path.EqualIgnoreCase("/api/status"))
        {
            var routes = _routes;
            json = new
            {
                uptime = Environment.TickCount64 / 1000,
                // 只取会话数量，避免序列化整个会话字典（内含 Socket，MulticastLoopback 在非组播 socket 上会抛异常）
                activeSessions = Sessions?.Count ?? 0,
                totalRequests = Interlocked.Read(ref _totalRequests),
                routeCount = routes?.Count ?? 0,
                port = Port,
                // P2：暴露当前配置来源，便于运维判断配置来自何处
                configSource = _configSource,
            }.ToJson();
        }
        else if (path.EqualIgnoreCase("/api/routes"))
        {
            var routes = _routes;
            if (routes != null)
            {
                var list = routes.Select(e => new
                {
                    e.Id,
                    e.Name,
                    e.Domain,
                    e.Path,
                    e.Methods,
                    e.Priority,
                    e.Enable,
                    cluster = e.ClusterName,
                }).ToList();
                json = list.ToJson();
            }
            else
            {
                json = "[]";
            }
        }
        else if (path.EqualIgnoreCase("/api/refresh"))
        {
            RefreshConfig();
            json = new { success = true, message = "配置已刷新" }.ToJson();
        }
        else
        {
            json = new { error = "unknown endpoint" }.ToJson();
        }

        var response = Encoding.UTF8.GetBytes(json);
        session.Send($"HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: {response.Length}\r\nConnection: close\r\n\r\n");
        session.Send(response);
        session.Dispose();

        AdminLog?.Info("Admin {0} from {1}", path, session.Remote);

        return true;
    }

    /// <summary>判断 Admin API 调用是否已授权。回环地址（本机运维工具）默认可访问；
    /// 非回环地址需配置 AdminToken 且请求携带匹配令牌（请求头 X-Gateway-Token、Authorization: Bearer，或浏览器 Basic 原生弹框）。
    /// 未配置 AdminToken 时，仅允许回环访问。</summary>
    private Boolean IsAdminAuthorized(HttpReverseSession session, HttpRequest request)
    {
        var token = StarGatewaySetting.Current.AdminToken;
        var remote = session.Remote + "";
        var isLoopback = IsLoopbackAddress(remote);

        // P2：配置了 AdminToken 时，所有来源（含本机回环）都必须携带匹配令牌，
        // 杜绝同主机其它进程/SSRF 无令牌调用 /api/refresh 或读取 /api/routes 拓扑；
        // 未配置 AdminToken 时，保持原行为：仅允许本机回环访问，外部拒绝
        if (token.IsNullOrEmpty()) return isLoopback;

        var provided = GetProvidedToken(request);
        return !provided.IsNullOrEmpty() && provided.EqualIgnoreCase(token);
    }

    /// <summary>判断远程地址是否为回环地址（127.0.0.0/8、::1，兼容 ::ffff:127.x 这类 IPv4 映射地址）</summary>
    private static Boolean IsLoopbackAddress(String remote)
    {
        if (remote.IsNullOrEmpty()) return false;
        // 先尝试整体解析（可能是裸 IP，无端口，如 ::1）
        if (IPAddress.TryParse(remote, out var addr) && IPAddress.IsLoopback(addr)) return true;
        // 去掉端口：[::1]:54321 / 127.0.0.1:54321
        var host = remote!;
        if (host.StartsWith("["))
        {
            var close = host.IndexOf(']');
            if (close > 0) host = host.Substring(1, close - 1);
        }
        else
        {
            var idx = host.LastIndexOf(':');
            if (idx > 0) host = host.Substring(0, idx);
        }
        // 处理 IPv4 映射的 IPv6 地址 ::ffff:127.0.0.1
        if (host.StartsWith("::ffff:", StringComparison.OrdinalIgnoreCase)) host = host.Substring(7);
        return IPAddress.TryParse(host, out addr) && IPAddress.IsLoopback(addr);
    }

    /// <summary>从请求头提取 AdminToken：依次支持 X-Gateway-Token、Authorization: Bearer、Authorization: Basic（浏览器原生弹框）。
    /// Basic 认证为 user:password 格式，取 password 部分作为 token；若不含冒号则整串作为 token。</summary>
    private static String GetProvidedToken(HttpRequest request)
    {
        if (request.Headers.TryGetValue("X-Gateway-Token", out var h) && !h.IsNullOrEmpty()) return h;
        if (request.Headers.TryGetValue("Authorization", out var a) && !a.IsNullOrEmpty())
        {
            var auth = a.Trim();
            if (auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) return auth.Substring(7);
            if (auth.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(auth.Substring(6)));
                    var sep = decoded.IndexOf(':');
                    if (sep < 0) return decoded;                       // 无冒号：整串当 token
                    var user = decoded.Substring(0, sep);
                    var pass = decoded.Substring(sep + 1);
                    // 密码非空优先用密码（用户名可任意）；密码为空则用用户名当 token，兼容“只有 token”的两种填法
                    return pass.IsNullOrEmpty() ? user : pass;
                }
                catch { return ""; }
            }
        }
        return "";
    }
    #endregion

    #region StarAgent 协同
    /// <summary>本地StarAgent地址。默认 http://127.0.0.1:5500</summary>
    public String AgentUrl { get; set; } = "http://127.0.0.1:5500";

    /// <summary>空闲超时。单位秒，超过该时间无流量的后端将被回收，默认900秒（15分钟）</summary>
    public Int32 IdleTimeout { get; set; } = 900;

    /// <summary>后端最后活动时间</summary>
    internal ConcurrentDictionary<String, DateTime> _lastActive = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>调用StarAgent启动服务（通过 UDP RPC）</summary>
    public async Task<Boolean> StartBackend(String address, String serviceName)
    {
        try
        {
            using var client = new ApiClient($"udp://127.0.0.1:{LocalStarClient.Port}")
            {
                Timeout = 5_000,
            };
            var rs = await client.InvokeAsync<ServiceOperationResult>("StartService", new { serviceName });
            AdminLog?.Info("StarAgent StartService {0}: {1}", serviceName, rs?.Message);
            return rs?.Success == true;
        }
        catch (Exception ex)
        {
            WriteError("调用StarAgent启动服务失败 {0}：{1}", serviceName, ex.Message);
            return false;
        }
    }

    /// <summary>调用StarAgent停止服务（通过 UDP RPC）</summary>
    public async Task<Boolean> StopBackend(String address, String serviceName)
    {
        try
        {
            using var client = new ApiClient($"udp://127.0.0.1:{LocalStarClient.Port}")
            {
                Timeout = 5_000,
            };
            var rs = await client.InvokeAsync<ServiceOperationResult>("StopService", new { serviceName });
            AdminLog?.Info("StarAgent StopService {0}: {1}", serviceName, rs?.Message);
            return rs?.Success == true;
        }
        catch (Exception ex)
        {
            WriteError("调用StarAgent停止服务失败 {0}：{1}", serviceName, ex.Message);
            return false;
        }
    }

    /// <summary>检查空闲后端并回收</summary>
    public async Task CheckIdleBackends()
    {
        var routes = _routes;
        if (routes == null) return;

        var now = DateTime.Now;
        foreach (var route in routes)
        {
            var nodes = GatewayNode.FindAllByClusterId(route.ClusterId);
            if (nodes == null) continue;

            foreach (var node in nodes)
            {
                if (!node.Enable) continue;

                var key = node.Address;
                if (_lastActive.TryGetValue(key, out var last))
                {
                    // 空闲时间超过阈值，且不是当前健康节点
                    if ((now - last).TotalSeconds > IdleTimeout && !node.IsHealthy)
                    {
                        AdminLog?.Info("空闲回收: {0} 超过 {1} 秒无活动", key, IdleTimeout);
                        // 可以调用StarAgent停止服务
                        // await StopBackend(key, serviceName);
                    }
                }
            }
        }
    }
    #endregion
}

/// <summary>Http反向代理会话</summary>
public class HttpReverseSession : ProxySession
{
    private String _targetAddress;
    private String _routeName;
    private IDisposable _span;

    /// <summary>是否为WebSocket升级请求</summary>
    private Boolean _isWebSocketUpgrade;

    /// <summary>是否已完成WebSocket升级（101响应已透传），后续帧走TCP透传</summary>
    private Boolean _upgraded;

    protected override void OnReceive(ReceivedEventArgs e)
    {
        if (Disposed) return;

        // WebSocket升级后，所有帧走TCP原始透传，跳过HTTP解析和日志
        if (_upgraded)
        {
            base.OnReceive(e);
            return;
        }

        var request = new HttpRequest();
        if (!request.Parse(e.Packet)) { base.OnReceive(e); return; }

        e.Message = request;

        var host = request.Headers["Host"] ?? "";
        var path = request.RequestUri?.OriginalString ?? "/";
        var method = request.Method ?? "GET";

        if (Host is not HttpReverseProxy proxy) { base.OnReceive(e); return; }

        // 统一异常保护：处理请求（含 Admin API、路由匹配、转发）时若抛异常，
        // 必须返回错误响应并关闭连接，否则客户端会一直挂起等待无返回。
        try
        {
            // 检测WebSocket升级请求
            var isUpgrade = request.Headers.TryGetValue("Upgrade", out var upgrade) &&
                            upgrade.EqualIgnoreCase("websocket") &&
                            request.Headers.TryGetValue("Connection", out var conn) &&
                            conn.IndexOf("upgrade", StringComparison.OrdinalIgnoreCase) >= 0;

            // 匹配路由（含Header匹配）
            var route = proxy.MatchRoute(host, path, method, request.Headers);

            // 如果是WebSocket升级请求，检查路由是否允许
            if (isUpgrade && route != null && !route.WebSocket)
            {
                // 路由禁止WebSocket，返回400
                proxy.AdminLog?.Info("WebSocket被路由 {0} 禁止: {1} {2}", route.Name, method, path);
                var body = "WebSocket upgrade not allowed for this route"u8.ToArray();
                Send($"HTTP/1.1 400 Bad Request\r\nContent-Type: text/plain\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n");
                Send(body);
                Dispose();
                return;
            }

            // 创建APM追踪span（仅首次请求，WebSocket升级后不再创建）
            var tracer = proxy.Tracer;
            if (tracer != null && !_upgraded)
            {
                var traceId = request.Headers["Trace-Id"] ?? request.Headers["traceparent"];
                var data = new { host, path, method };
                _span = tracer.NewSpan($"gateway:{method}:{path}", traceId != null ? new { traceId, data } : data);
            }

            // 检查Admin API
            if (proxy.HandleAdminRequest(this, path, request)) return;

            // 检查静态文件路由。路由开启了IsStaticRoute才走静态文件托管
            if (route != null && route.IsStaticRoute)
            {
                var staticRoot = route.StaticRoot;
                if (staticRoot.IsNullOrEmpty())
                {
                    // 开启了静态路由但没设置根目录，直接返回404
                    proxy.AdminLog?.Info("Static {0} {1} -> 404 静态路由未配置根目录", method, path);
                    Send("HTTP/1.1 404 Not Found\r\nContent-Length: 0\r\nConnection: close\r\n\r\n"u8.ToArray());
                    Dispose();
                    return;
                }

                if (Path.IsPathRooted(staticRoot))
                {
                    staticRoot = Path.GetFullPath(staticRoot);
                }
                else
                {
                    // 相对路径，基于当前工作目录
                    staticRoot = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), staticRoot));
                }

                if (proxy.StaticFiles.TryHandle(method, path, staticRoot, route.IndexFile ?? "index.html", route.DirectoryBrowse, route.SPAFallback, request.Headers, out var response))
                {
                    // 记录日志
                    proxy.AdminLog?.Info("Static {0} {1} [{2}]", method, path, route.Name);

                    // 创建APM追踪span
                    if (tracer != null)
                    {
                        _span?.Dispose();
                        _span = tracer.NewSpan($"static:{method}:{path}");
                    }

                    Send(response);
                    Dispose();
                    return;
                }

                // 静态文件处理失败（文件不存在等），不再继续转发
                proxy.AdminLog?.Info("Static {0} {1} -> 404 文件不存在", method, path);
                Send("HTTP/1.1 404 Not Found\r\nContent-Length: 0\r\nConnection: close\r\n\r\n"u8.ToArray());
                Dispose();
                return;
            }

            if (route != null)
            {
                var target = proxy.SelectNode(route, Remote?.Host);
                if (target != null)
                {
                    RemoteServerUri = target;
                    _targetAddress = target.ToString();
                    _routeName = route.Name;

                    // 追踪连接数
                    proxy.IncrementConnection(_targetAddress);
                    proxy._lastActive[_targetAddress] = DateTime.Now;

                    // 仅非WebSocket或首次升级请求记录日志
                    if (!isUpgrade)
                    {
                        proxy.AdminLog?.Info("{0} {1} -> {2}:{3} [{4}]", method, path, target.Host, target.Port, _routeName);
                    }
                    else
                    {
                        _isWebSocketUpgrade = true;
                        proxy.AdminLog?.Info("WS {0} {1} -> {2}:{3} [{4}]", method, path, target.Host, target.Port, _routeName);
                    }
                }
                else
                {
                    // 没有可用节点，尝试冷启动
                    proxy.WriteError("路由 {0} 没有可用的后端节点", route.Name);
                    _ = TryColdStart(proxy, route);
                }
            }
            else
            {
                // 未匹配路由，使用默认远程服务器
                if (proxy.RemoteServer != null) RemoteServerUri = proxy.RemoteServer;
            }

            // ---- 头部修改：StripPrefix & AddHeaders ----
            if (route != null)
            {
                var modified = false;

                // StripPrefix: 去除匹配路径前缀
                if (route.StripPrefix && !route.Path.IsNullOrEmpty())
                {
                    var prefix = route.Path.TrimEnd('*').TrimEnd('/');
                    if (!prefix.IsNullOrEmpty() && path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    {
                        var newPath = path.Substring(prefix.Length);
                        if (newPath.IsNullOrEmpty()) newPath = "/";
                        request.RequestUri = new Uri(newPath, UriKind.RelativeOrAbsolute);
                        modified = true;
                    }
                }

                // AddHeaders: 添加额外请求头
                if (!route.AddHeaders.IsNullOrEmpty())
                {
                    var headers = route.AddHeaderRules;
                    if (headers != null)
                    {
                        foreach (var kv in headers)
                        {
                            request.Headers[kv.Key] = kv.Value;
                        }
                        modified = true;
                    }
                }

                // 如果有修改，重建HTTP请求包
                if (modified)
                {
                    // 重建请求行和头部
                    var sb = Pool.StringBuilder.Get();
                    var requestUri = request.RequestUri?.OriginalString ?? path;
                    sb.Append($"{method} {requestUri} HTTP/1.1\r\n");
                    foreach (var kv in request.Headers)
                    {
                        if (!kv.Key.EqualIgnoreCase("Host"))
                            sb.Append($"{kv.Key}: {kv.Value}\r\n");
                    }
                    sb.Append("\r\n");

                    // 保留原始请求体（如果有）
                    var headerBytes = Encoding.UTF8.GetBytes(sb.ToString());
                    var body = e.Packet.Slice(headerBytes.Length);
                    // 替换包数据
                    e.Packet = new ArrayPacket(headerBytes.Concat(body.ToArray()).ToArray());
                    sb.TryDispose();
                }
            }

            // 转发请求到后端
            base.OnReceive(e);

            // WebSocket升级请求转发后，标记已升级，后续帧走TCP透传
            if (_isWebSocketUpgrade)
            {
                _upgraded = true;
            }
        }
        catch (Exception ex)
        {
            // 兜底：HandleAdminRequest 等任意环节抛异常时，返回 500 并释放连接，避免客户端挂起
            proxy?.WriteError("处理请求异常 {0} {1}：{2}", method, path, ex.Message);
            // 标记追踪片段为错误，使 APM 正确反映失败请求（否则 Dispose 会把异常请求记成成功）
            (_span as ISpan)?.SetError(ex, null);
            SendErrorResponse(500, "Internal Server Error");
        }
    }

    /// <summary>收到远程服务器返回的数据</summary>
    /// <param name="e"></param>
    protected override void OnReceiveRemote(ReceivedEventArgs e)
    {
        // WebSocket升级后，所有数据直接透传
        if (_upgraded)
        {
            base.OnReceiveRemote(e);
            return;
        }

        // 检查是否为101 Switching Protocols响应（WebSocket升级成功）
        if (_isWebSocketUpgrade)
        {
            var data = e.Packet.ToArray();
            var str = Encoding.UTF8.GetString(data);
            if (str.StartsWith("HTTP/1.1 101", StringComparison.Ordinal) ||
                str.StartsWith("HTTP/1.0 101", StringComparison.Ordinal))
            {
                // 标记升级完成，下次OnReceive直接走TCP透传
                _upgraded = true;
                if (Host is HttpReverseProxy proxy)
                {
                    proxy.AdminLog?.Info("WS 升级成功: {0}", _targetAddress);
                }
            }
        }

        base.OnReceiveRemote(e);
    }

    private async Task TryColdStart(HttpReverseProxy proxy, GatewayRoute route)
    {
        var cluster = route.Cluster;
        if (cluster == null) return;

        var nodes = GatewayNode.FindAllByClusterId(route.ClusterId);
        if (nodes == null || nodes.Count == 0) return;

        // 尝试唤醒第一个有问题的节点
        foreach (var node in nodes)
        {
            if (node.Enable && !node.IsHealthy)
            {
                proxy.AdminLog?.Info("冷启动: 尝试唤醒 {0} (路由: {1})", node.Address, route.Name);
                await proxy.StartBackend(node.Address, node.Name);
                break;
            }
        }
    }

    /// <summary>发送HTTP错误响应并关闭连接。用于异常处理兜底，避免客户端一直等待无返回</summary>
    /// <param name="code">HTTP状态码</param>
    /// <param name="reason">原因短语</param>
    private void SendErrorResponse(Int32 code, String reason)
    {
        try
        {
            if (Disposed) return;
            var body = Encoding.UTF8.GetBytes($"<html><body><h1>{code} {reason}</h1></body></html>");
            Send($"HTTP/1.1 {code} {reason}\r\nContent-Type: text/html; charset=utf-8\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n");
            Send(body);
        }
        catch
        {
            // 发送失败说明连接已断开，忽略即可
        }
        finally
        {
            Dispose();
        }
    }

    /// <summary>销毁</summary>
    protected override void Dispose(Boolean disposing)
    {
        if (_targetAddress != null && Host is HttpReverseProxy proxy)
        {
            proxy.DecrementConnection(_targetAddress);
        }

        _span.TryDispose();

        base.Dispose(disposing);
    }
}
