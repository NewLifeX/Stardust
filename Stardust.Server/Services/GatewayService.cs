using System;
using System.Collections.Generic;
using System.Linq;
using NewLife;
using NewLife.Log;
using Stardust.Data.Deployment;
using Stardust.Data.Gateway;

namespace Stardust.Server.Services;

/// <summary>网关配置服务。为 StarGateway 提供路由配置和集群信息</summary>
public class GatewayService
{
    #region 属性
    private readonly ITracer _tracer;
    #endregion

    #region 构造
    /// <summary>实例化网关配置服务</summary>
    public GatewayService(ITracer tracer) => _tracer = tracer;
    #endregion

    #region 方法
    /// <summary>获取全部启用的路由配置</summary>
    public IList<GatewayRoute> GetAllRoutes()
    {
        using var span = _tracer?.NewSpan("GatewayService-GetAllRoutes");

        return GatewayRoute.FindAllEnabled();
    }

    /// <summary>获取指定集群的完整配置</summary>
    /// <param name="clusterId">集群编号</param>
    public GatewayClusterInfo GetClusterInfo(Int32 clusterId)
    {
        using var span = _tracer?.NewSpan("GatewayService-GetClusterInfo", new { clusterId });

        var cluster = GatewayCluster.FindById(clusterId);
        if (cluster == null) return null;

        var nodes = GatewayNode.FindAllHealthyByCluster(clusterId);

        return new GatewayClusterInfo
        {
            Id = cluster.Id,
            Name = cluster.Name,
            LoadBalance = cluster.LoadBalance,
            HealthPath = cluster.HealthPath,
            HealthInterval = cluster.HealthInterval,
            HealthTimeout = cluster.HealthTimeout,
            UnhealthyThreshold = cluster.UnhealthyThreshold,
            HealthyThreshold = cluster.HealthyThreshold,
            SessionSticky = cluster.SessionSticky,
            Nodes = nodes.Select(e => new GatewayNodeInfo
            {
                Id = e.Id,
                Name = e.Name,
                Address = e.Address,
                Weight = e.Weight,
                IsHealthy = e.IsHealthy,
            }).ToList(),
        };
    }

    /// <summary>获取完整的网关运行时配置（路由表 + 集群 + 节点）</summary>
    public GatewayConfig GetAllConfig()
    {
        using var span = _tracer?.NewSpan("GatewayService-GetAllConfig");

        var config = new GatewayConfig();

        // 获取所有启用的路由
        var routes = GatewayRoute.FindAllEnabled();
        foreach (var route in routes)
        {
            var cluster = GetClusterInfo(route.ClusterId);
            if (cluster == null) continue;

            config.Routes.Add(new GatewayRouteInfo
            {
                Id = route.Id,
                Name = route.Name,
                Priority = route.Priority,
                Domain = route.Domain,
                Path = route.Path,
                Methods = route.Methods,
                Headers = route.Headers,
                StripPrefix = route.StripPrefix,
                AddHeaders = route.AddHeaders,
                Enable = route.Enable,
                WebSocket = route.WebSocket,
                IsStaticRoute = route.IsStaticRoute,
                StaticRoot = route.StaticRoot,
                IndexFile = route.IndexFile,
                DirectoryBrowse = route.DirectoryBrowse,
                SPAFallback = route.SPAFallback,
                Cluster = cluster,
            });
        }

        // 获取所有启用的证书（统一使用 SslCertificate）
        config.Certs = SslCertificate.FindAllEnabled().Select(e => new GatewayCertInfo
        {
            Id = e.Id,
            Name = e.Domain,
            Domain = e.Domain,
            CertFile = e.PemFile ?? e.CrtFile ?? e.PfxFile,
            KeyFile = e.KeyFile,
            // PfxPassword = e.PfxPassword,
        }).ToList();

        return config;
    }
    #endregion
}
