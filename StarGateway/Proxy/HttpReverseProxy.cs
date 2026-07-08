using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
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
using NewLife.Serialization;
using NewLife.Threading;
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
        LoadConfigWithFallback();

        // 加载SSL证书
        LoadCertificates();

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
            // 统一使用 SslCertificate（星尘部署中心证书管理）
            var certs = SslCertificate.FindAllEnabled();
            if (certs.Count == 0)
            {
                WriteLog("未配置SSL证书，仅支持HTTP");
                return;
            }

            // 加载第一个可用的证书（SNI多证书支持在后续版本完善）
            foreach (var certEntity in certs)
            {
                // 优先尝试PEM文件，其次PFX，最后CRT+KEY
                var file = certEntity.PemFile;
                if (file.IsNullOrEmpty() || !File.Exists(file))
                {
                    // 尝试PFX
                    if (!certEntity.PfxFile.IsNullOrEmpty() && File.Exists(certEntity.PfxFile))
                    {
                        try
                        {
                            var pfxPassword = certEntity.PfxPassword;
                            var cert = pfxPassword.IsNullOrEmpty()
                                ? new X509Certificate2(certEntity.PfxFile)
                                : new X509Certificate2(certEntity.PfxFile, pfxPassword);
                            Certificate = cert;
                            SslProtocol = SslProtocols.Tls12;
                            WriteLog("加载SSL证书(PFX): {0} -> {1}", certEntity.Domain, cert.Subject);
                            break;
                        }
                        catch (Exception exPfx)
                        {
                            WriteError("加载PFX证书 {0} 失败：{1}", certEntity.PfxFile, exPfx.Message);
                            continue;
                        }
                    }
                    // 尝试CRT+KEY
                    if (!certEntity.CrtFile.IsNullOrEmpty() && File.Exists(certEntity.CrtFile))
                    {
                        file = certEntity.CrtFile;
                    }
                    else
                    {
                        continue;
                    }
                }

                try
                {
                    // 加载PEM/CRT格式证书
#pragma warning disable SYSLIB0057
                    var cert = new X509Certificate2(file);
#pragma warning restore SYSLIB0057
                    Certificate = cert;
                    SslProtocol = SslProtocols.Tls12;
                    WriteLog("加载SSL证书: {0} -> {1}", certEntity.Domain, cert.Subject);
                    break;
                }
                catch (Exception ex)
                {
                    WriteError("加载证书 {0} 失败：{1}", file, ex.Message);
                }
            }
        }
        catch (Exception ex)
        {
            WriteError("加载SSL证书配置失败：{0}", ex.Message);
        }
    }
    #endregion

    #region 配置加载（多级兜底）
    protected virtual void LoadConfigWithFallback()
    {
        // 1. 从StarServer拉取配置
        try
        {
            var set = StarGatewaySetting.Current;
            if (!set.StarServer.IsNullOrEmpty())
            {
                LoadConfigFromServer();
                return;
            }
        }
        catch (Exception ex)
        {
            WriteError("从StarServer加载配置失败：{0}", ex.Message);
        }

        // 2. 从数据库加载
        try
        {
            var routes = GatewayRoute.FindAllEnabled();
            if (routes.Count > 0)
            {
                Interlocked.Exchange(ref _routes, routes);
                WriteLog("从数据库加载路由配置完成，共 {0} 条路由", routes.Count);
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

    protected virtual void LoadConfigFromServer()
    {
        var set = StarGatewaySetting.Current;
        var url = set.StarServer;
        if (url.IsNullOrEmpty())
        {
            LoadConfig();
            return;
        }

        // 尝试通过 StarServer API 获取配置（需将 GatewayConfig DTO 移动到 Stardust.Data）
        // 当前版本使用数据库共享模式作为主要配置源，API 拉取为预留扩展点
        // 后续可参考：var config = await StarClient?.Client?.InvokeAsync<GatewayConfig>("Gateway/config");

        LoadConfig();
    }

    protected virtual void LoadConfigFromLocalFile()
    {
        var file = StarGatewaySetting.Current.LocalConfigFile;
        if (file.IsNullOrEmpty() || !File.Exists(file)) return;

        var json = File.ReadAllText(file);
        if (json.IsNullOrEmpty()) return;

        // 简单解析本地配置文件
        // 格式: [{ "name":"route1", "domain":"*.example.com", "target":"http://localhost:5000" }]
        var list = JsonParser.Decode(json) as IList<Object>;
        if (list == null || list.Count == 0) return;

        WriteLog("从本地文件 {0} 加载路由配置，共 {1} 条", file, list.Count);
    }

    protected virtual void LoadConfig()
    {
        try
        {
            var routes = GatewayRoute.FindAllEnabled();
            Interlocked.Exchange(ref _routes, routes);
        }
        catch (Exception ex)
        {
            WriteError("加载路由配置失败：{0}", ex.Message);
        }
    }

    private async Task DoRefreshConfig(Object state)
    {
        LoadConfigWithFallback();

        // 配置刷新时同时刷新证书（证书热更新）
        LoadCertificates();

        await Task.CompletedTask;
    }

    public void RefreshConfig() => LoadConfigWithFallback();
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

        Interlocked.Increment(ref _totalRequests);

        var json = "";
        if (path.EqualIgnoreCase("/api/status"))
        {
            var routes = _routes;
            json = new
            {
                uptime = Environment.TickCount64 / 1000,
                activeSessions = Sessions,
                totalRequests = Interlocked.Read(ref _totalRequests),
                routeCount = routes?.Count ?? 0,
                port = Port,
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
    #endregion

    #region StarAgent 协同
    /// <summary>本地StarAgent地址。默认 http://127.0.0.1:5500</summary>
    public String AgentUrl { get; set; } = "http://127.0.0.1:5500";

    /// <summary>空闲超时。单位秒，超过该时间无流量的后端将被回收，默认900秒（15分钟）</summary>
    public Int32 IdleTimeout { get; set; } = 900;

    /// <summary>后端最后活动时间</summary>
    internal ConcurrentDictionary<String, DateTime> _lastActive = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>调用StarAgent启动服务</summary>
    public async Task<Boolean> StartBackend(String address, String serviceName)
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            var url = $"{AgentUrl}/api/StartService?serviceName={serviceName}";
            var rs = await client.GetStringAsync(url);
            AdminLog?.Info("StarAgent StartService {0}: {1}", serviceName, rs);
            return true;
        }
        catch (Exception ex)
        {
            WriteError("调用StarAgent启动服务失败 {0}：{1}", serviceName, ex.Message);
            return false;
        }
    }

    /// <summary>调用StarAgent停止服务</summary>
    public async Task<Boolean> StopBackend(String address, String serviceName)
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            var url = $"{AgentUrl}/api/StopService?serviceName={serviceName}";
            var rs = await client.GetStringAsync(url);
            AdminLog?.Info("StarAgent StopService {0}: {1}", serviceName, rs);
            return true;
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
