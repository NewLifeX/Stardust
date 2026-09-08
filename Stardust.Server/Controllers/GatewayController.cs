using System;
using Microsoft.AspNetCore.Mvc;
using NewLife;
using NewLife.Remoting;
using NewLife.Remoting.Extensions;
using NewLife.Remoting.Services;
using Stardust.Data;
using Stardust.Data.Gateway;
using Stardust.Server.Services;

namespace Stardust.Server.Controllers;

/// <summary>网关控制器。为 StarGateway 实例提供配置查询接口（安全V-10：需应用身份，防公网拓扑泄露）</summary>
[Route("[controller]")]
public class GatewayController : ControllerBase
{
    #region 属性
    private readonly GatewayService _gatewayService;
    private readonly AppTokenService _appTokenService;
    private readonly ITokenService _tokenService;
    private readonly StarServerSetting _setting;
    #endregion

    #region 构造
    /// <summary>实例化网关控制器</summary>
    public GatewayController(GatewayService gatewayService, AppTokenService appTokenService, ITokenService tokenService, StarServerSetting setting)
    {
        _gatewayService = gatewayService;
        _appTokenService = appTokenService;
        _tokenService = tokenService;
        _setting = setting;
    }
    #endregion

    #region 接口
    /// <summary>获取完整的网关配置（路由表 + 集群 + 节点 + 证书）</summary>
    /// <returns>网关配置</returns>
    [HttpGet("config")]
    public GatewayConfig GetConfig(String appId = null, String secret = null, String clientId = null, String token = null)
    {
        if (!Valid(appId, secret, token))
        {
            Response.StatusCode = 401;
            return null;
        }
        return _gatewayService.GetAllConfig();
    }

    /// <summary>获取指定集群的配置</summary>
    /// <param name="clusterId">集群编号</param>
    /// <returns>集群配置</returns>
    [HttpGet("cluster")]
    public GatewayClusterInfo GetCluster(Int32 clusterId, String appId = null, String secret = null, String clientId = null, String token = null)
    {
        if (!Valid(appId, secret, token))
        {
            Response.StatusCode = 401;
            return null;
        }
        return _gatewayService.GetClusterInfo(clusterId);
    }

    /// <summary>获取所有启用的路由</summary>
    /// <returns>路由列表</returns>
    [HttpGet("routes")]
    public IList<Data.Gateway.GatewayRoute> GetRoutes(String appId = null, String secret = null, String clientId = null, String token = null)
    {
        if (!Valid(appId, secret, token))
        {
            Response.StatusCode = 401;
            return null;
        }
        return _gatewayService.GetAllRoutes();
    }
    #endregion

    #region 辅助
    /// <summary>校验调用方。优先应用令牌；否则应用标识+密钥（StarGateway 走此路径）。失败返回 false（调用方回退本地配置）</summary>
    private Boolean Valid(String appId, String secret, String token)
    {
        try
        {
            // 令牌优先
            if (!token.IsNullOrEmpty())
            {
                var (jwt, ex) = _tokenService.DecodeToken(token);
                if (ex != null) return false;

                var app = App.FindByName(jwt?.Subject);
                if (app == null || !app.Enable || app.Secret.IsNullOrEmpty()) return false;
                return true;
            }

            // 应用标识+密钥（StarGateway 已按此形态调用）
            var ip = HttpContext.GetUserHost();
            _appTokenService.Authorize(appId, secret, _setting.AppAutoRegister, ip);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
    #endregion
}
