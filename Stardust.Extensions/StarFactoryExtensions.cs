using System;
using System.ComponentModel;
using System.Linq;
using System.Net.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using NewLife;
using NewLife.Caching;
using NewLife.Configuration;
using NewLife.Log;
using NewLife.Reflection;
using NewLife.Remoting.Clients;
using Stardust;
using Stardust.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>星尘工厂扩展</summary>
public static class StarFactoryExtensions
{
    /// <summary>添加星尘，提供监控、配置和服务注册发现能力。先后从参数配置和本机StarAgent读取星尘平台地址</summary>
    /// <param name="services"></param>
    /// <param name="appId"></param>
    /// <returns></returns>
    public static StarFactory AddStardust(this IServiceCollection services, String appId) => AddStardust(services, null, appId, null);

    /// <summary>添加星尘，提供监控、配置和服务注册发现能力。先后从参数配置和本机StarAgent读取星尘平台地址</summary>
    /// <param name="services"></param>
    /// <param name="server">服务端地址。为空时先后读取appsettings.json、本地StarAgent、star.config，初始值为空，不连接服务端</param>
    /// <param name="appId">应用标识。为空时读取star.config，初始值为入口程序集名称</param>
    /// <param name="secret">应用密钥。为空时读取star.config，初始值为空</param>
    /// <returns></returns>
    public static StarFactory AddStardust(this IServiceCollection services, String? server = null, String? appId = null, String? secret = null)
    {
        var star = new StarFactory(server, appId, secret);

        // 替换为混合配置提供者，优先本地配置
        var old = services.LastOrDefault(e => e.ServiceType == typeof(IConfigProvider))?.ImplementationInstance as IConfigProvider;
        old ??= JsonConfigProvider.LoadAppSettings();
        star.SetLocalConfig(old);

        services.AddSingleton(star);
        services.AddSingleton(p => star.Tracer ?? DefaultTracer.Instance ?? (DefaultTracer.Instance ??= new DefaultTracer()));
        //services.AddSingleton(p => star.Config);
        services.AddSingleton(p => star.Service!);
        services.AddSingleton(p => (star.Service as IEventProvider)!);
        services.AddSingleton(p => (star.Service as ICommandClient)!);

        // 替换为混合配置提供者，优先本地配置
        //services.Replace(new ServiceDescriptor(typeof(IConfigProvider), p => star.Config, ServiceLifetime.Singleton));
        //var old = services.LastOrDefault(e => e.ServiceType == typeof(IConfigProvider))?.ImplementationInstance as IConfigProvider;
        //old ??= JsonConfigProvider.LoadAppSettings();
        if (services.Any(e => e.ServiceType == typeof(IConfigProvider)))
            services.Replace(new ServiceDescriptor(typeof(IConfigProvider), p => star.GetConfig()!, ServiceLifetime.Singleton));
        else
            services.TryAddSingleton(p => star.GetConfig()!);

        // 分布式缓存
        //services.Replace(new ServiceDescriptor(typeof(CacheService), p => new RedisCacheService(p), ServiceLifetime.Singleton));
        services.TryAddSingleton<ICacheProvider, CacheProvider>();

        //services.AddHostedService<StarService>();
        services.TryAddSingleton(XTrace.Log);

        services.AddSingleton(serviceProvider =>
        {
            var server = serviceProvider.GetRequiredService<IServer>();
            return server.Features.Get<IServerAddressesFeature>()!;
        });

        return star;
    }

    /// <summary>使用星尘，注入跟踪中间件。不需要跟魔方一起使用</summary>
    /// <param name="app"></param>
    /// <returns></returns>
    public static IApplicationBuilder UseStardust(this IApplicationBuilder app)
    {
        // 如果已引入追踪中间件，则这里不再引入
        if (!app.Properties.ContainsKey(nameof(TracerMiddleware)))
        {
            var provider = app.ApplicationServices;
            var tracer = provider.GetRequiredService<ITracer>();

            TracerMiddleware.Tracer ??= tracer;
            if (TracerMiddleware.Tracer != null) app.UseMiddleware<TracerMiddleware>();

            app.Properties[nameof(TracerMiddleware)] = typeof(TracerMiddleware);
        }

        //app.UseMiddleware<RegistryMiddleware>();

        return app;
    }

    /// <summary>发布服务到注册中心</summary>
    /// <param name="app"></param>
    /// <param name="serviceName">服务名</param>
    /// <param name="address">服务地址</param>
    /// <param name="tag">特性标签</param>
    /// <param name="health">健康监测接口地址</param>
    /// <returns></returns>
    public static IApplicationBuilder RegisterService(this IApplicationBuilder app, String serviceName, String? address = null, String? tag = null, String? health = null)
    {
        var star = app.ApplicationServices.GetRequiredService<StarFactory>();
        if (star == null) throw new InvalidOperationException("未注册StarFactory，需要AddStardust注册。");

        if (serviceName.IsNullOrEmpty()) serviceName = AssemblyX.Entry?.Name!;

        // 启动的时候注册服务
        var lifetime = app.ApplicationServices.GetRequiredService<IHostApplicationLifetime>();
        lifetime.ApplicationStarted.Register(() =>
        {
            DefaultSpan.Current = null;
            try
            {
                /*
                 * 服务地址获取逻辑：
                 * 1，外部传参 address
                 * 2，配置指定 ServiceAddress
                 * 3，获取监听地址，若未改变，则使用 AccessAddress
                 * 4，若监听地址已改变，则使用监听地址
                 * 5，若监听地址获取失败，则注册回调
                 */
                //var set = NewLife.Setting.Current;
                //if (address.IsNullOrEmpty()) address = set.ServiceAddress;
                if (address.IsNullOrEmpty())
                {
                    // 本地监听地址，属于内部地址
                    var feature = app.ServerFeatures.Get<IServerAddressesFeature>();
                    address = feature?.Addresses.Join(",");

                    if (address.IsNullOrEmpty())
                    {
                        if (feature == null) throw new Exception("尘埃客户端未能取得本地服务地址。");

                        star.Service?.Register(serviceName, () => feature?.Addresses.Join(","), tag, health);

                        return;
                    }
                }
                star.Service?.RegisterAsync(serviceName, address, tag, health).Wait(5_000);
            }
            catch (HttpRequestException) { }
            catch (AggregateException ex) when (ex.InnerException is HttpRequestException) { }
            catch (Exception ex)
            {
                XTrace.WriteException(ex);
            }
        });

        // 停止的时候移除服务
        NewLife.Model.Host.RegisterExit(() =>
        {
            DefaultSpan.Current = null;

            // 从注册中心释放服务提供者和消费者
            star.Service.TryDispose();
        });

        return app;
    }
}