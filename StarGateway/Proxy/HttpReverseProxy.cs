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
using NewLife.Http;
using NewLife.Log;
using NewLife.Net;
using NewLife.Serialization;
using NewLife.Threading;
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
            var certs = GatewayCert.FindAllEnabled();
            if (certs.Count == 0)
            {
                WriteLog("未配置SSL证书，仅支持HTTP");
                return;
            }

            // 加载第一个可用的证书
            foreach (var certEntity in certs)
            {
                var file = certEntity.CertFile;
                if (file.IsNullOrEmpty() || !File.Exists(file)) continue;

                try
                {
                    // 尝试加载PEM格式证书
#pragma warning disable SYSLIB0057
                    var cert = new X509Certificate2(file);
#pragma warning restore SYSLIB0057
                    Certificate = cert;
                    SslProtocol = SslProtocols.Tls12;
                    WriteLog("加载SSL证书: {0} -> {1}", certEntity.Name, cert.Subject);
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
        // TODO: 通过 ApiHttpClient 连接 StarServer 拉取配置
        // var client = new ApiHttpClient(set.StarServer);
        // var config = await client.GetAsync<GatewayConfig>("/gateway/config");
        WriteLog("StarServer配置下发功能已预留（地址：{0}）", StarGatewaySetting.Current.StarServer);
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
            var task = tcp.ConnectAsync(uri.Address, uri.Port);
            return task.Wait(3000);
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
        if (!path.StartsWith("/__admin/", StringComparison.OrdinalIgnoreCase)) return false;

        Interlocked.Increment(ref _totalRequests);

        var json = "";
        if (path.EqualIgnoreCase("/__admin/status"))
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
        else if (path.EqualIgnoreCase("/__admin/routes"))
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
        else if (path.EqualIgnoreCase("/__admin/refresh"))
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

    protected override void OnReceive(ReceivedEventArgs e)
    {
        if (Disposed) return;

        var request = new HttpRequest();
        if (!request.Parse(e.Packet)) { base.OnReceive(e); return; }

        e.Message = request;

        var host = request.Headers["Host"] ?? "";
        var path = request.RequestUri?.OriginalString ?? "/";
        var method = request.Method ?? "GET";

        if (Host is not HttpReverseProxy proxy) { base.OnReceive(e); return; }

        // 检查Admin API
        if (proxy.HandleAdminRequest(this, path, request)) return;

        // 匹配路由
        var route = proxy.MatchRoute(host, path, method);
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
                proxy.AdminLog?.Info("{0} {1} -> {2}:{3} [{4}]", method, path, target.Host, target.Port, _routeName);
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

        base.OnReceive(e);
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

        base.Dispose(disposing);
    }
}
