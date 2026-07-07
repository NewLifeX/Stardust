using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
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

    private TimerX _timer;

    /// <summary>总请求数</summary>
    internal Int64 _totalRequests;

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
        LoadConfig();
        _timer = new TimerX(DoRefreshConfig, null, ConfigRefreshInterval * 1000, ConfigRefreshInterval * 1000) { Async = true };
        WriteLog("Http反向代理启动，监听端口：{0}，路由数：{1}", Port, _routes?.Count ?? 0);
        base.OnStart();
    }
    #endregion

    #region 配置加载
    protected virtual void LoadConfig()
    {
        try
        {
            var routes = GatewayRoute.FindAllEnabled();
            Interlocked.Exchange(ref _routes, routes);
            WriteLog("加载路由配置完成，共 {0} 条路由", routes.Count);
        }
        catch (Exception ex)
        {
            WriteError("加载路由配置失败：{0}", ex.Message);
        }
    }

    private async Task DoRefreshConfig(Object state)
    {
        LoadConfig();
        await Task.CompletedTask;
    }

    public void RefreshConfig() => LoadConfig();
    #endregion

    #region 会话管理
    protected override INetSession CreateSession(ISocketSession session) => new HttpReverseSession { Host = this };
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

    public NetUri SelectNode(GatewayRoute route)
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
                selected = SelectIPHash(nodes, "");
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

    private static GatewayNode SelectLeastConnection(IList<GatewayNode> nodes) => SelectRoundRobin(nodes);

    private static GatewayNode SelectIPHash(IList<GatewayNode> nodes, String ip)
    {
        var hash = ip.IsNullOrEmpty() ? 0 : ip.GetHashCode();
        var index = Math.Abs(hash) % nodes.Count;
        return nodes[index];
    }
    #endregion

    #region Admin API
    /// <summary>处理管理请求（返回true表示已处理）</summary>
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

        // 发送响应
        var response = Encoding.UTF8.GetBytes(json);
        session.Send($"HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: {response.Length}\r\nConnection: close\r\n\r\n");
        session.Send(response);
        session.Dispose();

        AdminLog?.Info("Admin {0} from {1}", path, session.Remote);

        return true;
    }
    #endregion
}

/// <summary>Http反向代理会话。处理客户端请求，匹配路由并转发到目标集群</summary>
public class HttpReverseSession : ProxySession
{
    /// <summary>收到客户端发来的数据</summary>
    protected override void OnReceive(ReceivedEventArgs e)
    {
        if (Disposed) return;

        // 解析HTTP请求
        var request = new HttpRequest();
        if (!request.Parse(e.Packet)) { base.OnReceive(e); return; }

        e.Message = request;

        var host = request.Headers["Host"] ?? "";
        var path = request.RequestUri?.OriginalString ?? "/";
        var method = request.Method ?? "GET";

        if (Host is not HttpReverseProxy proxy) { base.OnReceive(e); return; }

        // 检查Admin API
        if (proxy.HandleAdminRequest(this, path, request))
        {
            proxy.AdminLog?.Info("{0} {1} {2} {3}", method, path, host, Remote);
            return;
        }

        // 匹配路由（域名/路径/方法）
        var route = proxy.MatchRoute(host, path, method);
        if (route != null)
        {
            var target = proxy.SelectNode(route);
            if (target != null)
            {
                RemoteServerUri = target;
                Interlocked.Increment(ref proxy._totalRequests);
                proxy.AdminLog?.Info("{0} {1} -> {2}:{3} [{4}]", method, path, target.Host, target.Port, route.Name);
            }
            else
            {
                proxy.WriteError("路由 {0} 没有可用的后端节点", route.Name);
            }
        }
        else
        {
            // 未匹配路由，使用默认远程服务器
            if (proxy.RemoteServer != null) RemoteServerUri = proxy.RemoteServer;
        }

        base.OnReceive(e);
    }
}
