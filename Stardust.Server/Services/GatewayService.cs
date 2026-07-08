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
        }).ToList();

        return config;
    }
    #endregion
}

#region 配置模型
/// <summary>网关完整配置</summary>
public class GatewayConfig
{
    /// <summary>路由列表</summary>
    public IList<GatewayRouteInfo> Routes { get; set; } = [];

    /// <summary>证书列表</summary>
    public IList<GatewayCertInfo> Certs { get; set; } = [];
}

/// <summary>路由配置</summary>
public class GatewayRouteInfo
{
    /// <summary>编号</summary>
    public Int32 Id { get; set; }

    /// <summary>名称</summary>
    public String Name { get; set; }

    /// <summary>优先级</summary>
    public Int32 Priority { get; set; }

    /// <summary>域名匹配</summary>
    public String Domain { get; set; }

    /// <summary>路径匹配</summary>
    public String Path { get; set; }

    /// <summary>HTTP方法</summary>
    public String Methods { get; set; }

    /// <summary>请求头匹配</summary>
    public String Headers { get; set; }

    /// <summary>去除前缀</summary>
    public Boolean StripPrefix { get; set; }

    /// <summary>添加请求头</summary>
    public String AddHeaders { get; set; }

    /// <summary>目标集群</summary>
    public GatewayClusterInfo Cluster { get; set; }
}

/// <summary>集群配置</summary>
public class GatewayClusterInfo
{
    /// <summary>编号</summary>
    public Int32 Id { get; set; }

    /// <summary>名称</summary>
    public String Name { get; set; }

    /// <summary>负载均衡算法</summary>
    public String LoadBalance { get; set; }

    /// <summary>健康检查路径</summary>
    public String HealthPath { get; set; }

    /// <summary>健康检查间隔（秒）</summary>
    public Int32 HealthInterval { get; set; }

    /// <summary>健康检查超时（毫秒）</summary>
    public Int32 HealthTimeout { get; set; }

    /// <summary>不健康阈值</summary>
    public Int32 UnhealthyThreshold { get; set; }

    /// <summary>健康阈值</summary>
    public Int32 HealthyThreshold { get; set; }

    /// <summary>会话保持</summary>
    public Boolean SessionSticky { get; set; }

    /// <summary>后端节点列表</summary>
    public IList<GatewayNodeInfo> Nodes { get; set; } = [];
}

/// <summary>节点配置</summary>
public class GatewayNodeInfo
{
    /// <summary>编号</summary>
    public Int32 Id { get; set; }

    /// <summary>名称</summary>
    public String Name { get; set; }

    /// <summary>地址</summary>
    public String Address { get; set; }

    /// <summary>权重</summary>
    public Int32 Weight { get; set; }

    /// <summary>是否健康</summary>
    public Boolean IsHealthy { get; set; }
}

/// <summary>证书配置</summary>
public class GatewayCertInfo
{
    /// <summary>编号</summary>
    public Int32 Id { get; set; }

    /// <summary>名称</summary>
    public String Name { get; set; }

    /// <summary>域名</summary>
    public String Domain { get; set; }

    /// <summary>证书文件</summary>
    public String CertFile { get; set; }

    /// <summary>私钥文件</summary>
    public String KeyFile { get; set; }
}
#endregion
