using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using NewLife;
using NewLife.Http;
using NewLife.Net;
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
    public GatewayRoute MatchRoute(String domain, String path, String method)
    {
        var routes = _routes;
        if (routes == null || routes.Count == 0) return null;

        foreach (var route in routes)
        {
            if (route.Match(domain, path, method)) return route;
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
}

/// <summary>Http反向代理会话。处理客户端请求，匹配路由并转发到目标集群</summary>
public class HttpReverseSession : ProxySession
{
    /// <summary>原始主机</summary>
    public String RawHost { get; set; }

    /// <summary>请求地址</summary>
    public Uri LocalUri { get; set; }

    /// <summary>远程地址</summary>
    public Uri RemoteUri { get; set; }

    /// <summary>收到客户端发来的数据</summary>
    protected override void OnReceive(ReceivedEventArgs e)
    {
        if (Disposed) return;

        // 解析HTTP请求，用于路由匹配
        var request = new HttpRequest();
        if (request.Parse(e.Packet))
        {
            e.Message = request;

            var host = request.Headers["Host"];
            var path = request.RequestUri?.OriginalString ?? "/";
            var method = request.Method;

            // 匹配路由
            if (Host is HttpReverseProxy proxy)
            {
                var route = proxy.MatchRoute(host, path, method);
                if (route != null)
                {
                    // 选择后端节点
                    var target = proxy.SelectNode(route);
                    if (target != null)
                    {
                        RemoteServerUri = target;
                        WriteDebugLog("路由匹配: {0} -> {1}:{2}", route.Name, target.Host, target.Port);
                    }
                    else
                    {
                        WriteError("路由 {0} 没有可用的后端节点", route.Name);
                    }
                }
                else
                {
                    // 未匹配路由，使用默认远程服务器
                    if (proxy.RemoteServer != null) RemoteServerUri = proxy.RemoteServer;
                }
            }
        }

        base.OnReceive(e);
    }
}
