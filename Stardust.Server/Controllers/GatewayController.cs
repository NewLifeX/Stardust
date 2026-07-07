using System;
using Microsoft.AspNetCore.Mvc;
using NewLife;
using NewLife.Remoting;
using Stardust.Server.Services;

namespace Stardust.Server.Controllers;

/// <summary>网关控制器。为 StarGateway 实例提供配置查询接口</summary>
[Route("[controller]")]
public class GatewayController : ControllerBase
{
    #region 属性
    private readonly GatewayService _gatewayService;
    #endregion

    #region 构造
    /// <summary>实例化网关控制器</summary>
    public GatewayController(GatewayService gatewayService) => _gatewayService = gatewayService;
    #endregion

    #region 接口
    /// <summary>获取完整的网关配置（路由表 + 集群 + 节点 + 证书）</summary>
    /// <returns>网关配置</returns>
    [HttpGet("config")]
    public GatewayConfig GetConfig() => _gatewayService.GetAllConfig();

    /// <summary>获取指定集群的配置</summary>
    /// <param name="clusterId">集群编号</param>
    /// <returns>集群配置</returns>
    [HttpGet("cluster")]
    public GatewayClusterInfo GetCluster(Int32 clusterId) => _gatewayService.GetClusterInfo(clusterId);

    /// <summary>获取所有启用的路由</summary>
    /// <returns>路由列表</returns>
    [HttpGet("routes")]
    public IList<Data.Gateway.GatewayRoute> GetRoutes() => _gatewayService.GetAllRoutes();
    #endregion
}
