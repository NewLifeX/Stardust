using System;
using NewLife;
﻿using NewLife.Configuration;
using NewLife.Model;

namespace Stardust;

/// <summary>星尘辅助类</summary>
public static class StarHelper
{
    /// <summary>修正客户端标识。缺少主机段（形如 <c>@1234</c>）时用机器名补足</summary>
    /// <remarks>
    /// <c>Runtime.ClientId</c> 为 <c>ip@pid</c>，网络未就绪时退化为 <c>@pid</c>。
    /// 没有主机段的标识无法按主机归组在线实例，进程重启后也完全变样，服务端会把每次重连当成新实例。
    /// </remarks>
    /// <param name="clientId">原始客户端标识</param>
    /// <returns>补齐主机段后的标识</returns>
    public static String? FixClientId(String? clientId)
    {
        if (clientId.IsNullOrEmpty() || clientId[0] != '@') return clientId;

        return $"{Environment.MachineName}{clientId}";
    }

    /// <summary>添加星尘，提供监控、配置和服务注册发现能力。先后从参数配置和本机StarAgent读取星尘平台地址</summary>
    /// <param name="services"></param>
    /// <param name="appId"></param>
    /// <returns></returns>
    public static StarFactory AddStardust(this IObjectContainer services, String appId) => AddStardust(services, new StarOptions { AppId = appId });

    /// <summary>添加星尘，提供监控、配置和服务注册发现能力。先后从参数配置和本机StarAgent读取星尘平台地址</summary>
    /// <param name="services"></param>
    /// <param name="server">服务端地址。为空时先后读取appsettings.json、本地StarAgent、star.config，初始值为空，不连接服务端</param>
    /// <param name="appId">应用标识。为空时读取star.config，初始值为入口程序集名称</param>
    /// <param name="secret">应用密钥。为空时读取star.config，初始值为空</param>
    /// <returns></returns>
    public static StarFactory AddStardust(this IObjectContainer services, String? server = null, String? appId = null, String? secret = null)
        => AddStardust(services, new StarOptions { Server = server, AppId = appId, Secret = secret });

    /// <summary>添加星尘，并指定启动选项</summary>
    /// <param name="services"></param>
    /// <param name="options">启动选项。包含服务端地址、应用标识、密钥、延迟启动与客户端行为</param>
    /// <returns></returns>
    public static StarFactory AddStardust(this IObjectContainer services, StarOptions options)
        => AddStardustCore(services, options);

    private static StarFactory AddStardustCore(IObjectContainer services, StarOptions options)
    {
        var star = new StarFactory(options);

        // 替换为混合配置提供者，优先本地配置
        var old = JsonConfigProvider.LoadAppSettings();
        star.SetLocalConfig(old);

        star.Register(services);

        return star;
    }
}