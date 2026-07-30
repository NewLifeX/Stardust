using System;
using System.Collections.Generic;

namespace Stardust.Data.Gateway;

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

    /// <summary>是否启用</summary>
    public Boolean Enable { get; set; } = true;

    /// <summary>是否允许WebSocket升级</summary>
    public Boolean WebSocket { get; set; }

    /// <summary>是否静态文件路由（开启后不走反向代理，改为托管本地静态文件）</summary>
    public Boolean IsStaticRoute { get; set; }

    /// <summary>静态文件根目录</summary>
    public String StaticRoot { get; set; }

    /// <summary>默认首页</summary>
    public String IndexFile { get; set; }

    /// <summary>是否允许目录浏览</summary>
    public Boolean DirectoryBrowse { get; set; }

    /// <summary>SPA回退（文件不存在时回退到首页，支持前端 history 路由）</summary>
    public Boolean SPAFallback { get; set; }

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

    /// <summary>PFX 证书密码（PEM/CRT 证书为空）</summary>
    public String PfxPassword { get; set; }
}
