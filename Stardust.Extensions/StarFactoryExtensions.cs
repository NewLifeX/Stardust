using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Hosting;
using NewLife;
using NewLife.Log;
using NewLife.Reflection;
using Stardust;
using Stardust.Extensions;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>星尘工厂扩展</summary>
    public static class StarFactoryExtensions
    {
        /// <summary>添加星尘</summary>
        /// <param name="services"></param>
        /// <param name="appId"></param>
        /// <returns></returns>
        public static StarFactory AddStardust(this IServiceCollection services, String appId)
        {
            var star = new StarFactory(null, appId, null);

            services.AddSingleton(star);
            services.AddSingleton(P => star.Tracer ?? DefaultTracer.Instance ?? (DefaultTracer.Instance ??= new DefaultTracer()));
            services.AddSingleton(P => star.Config);
            services.AddSingleton(p => star.Service);

            //services.AddHostedService<StarService>();

            services.AddSingleton(serviceProvider =>
            {
                var server = serviceProvider.GetRequiredService<IServer>();
                return server.Features.Get<IServerAddressesFeature>();
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

                if (TracerMiddleware.Tracer == null) TracerMiddleware.Tracer = tracer;
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
        public static IApplicationBuilder RegisterService(this IApplicationBuilder app, String serviceName, String address = null, String tag = null, String health = null)
        {
            var star = app.ApplicationServices.GetRequiredService<StarFactory>();
            if (star == null) throw new InvalidOperationException("未注册StarFactory，需要AddStardust注册。");

            if (serviceName.IsNullOrEmpty()) serviceName = AssemblyX.Entry.Name;

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
                    var set = StarSetting.Current;
                    if (address.IsNullOrEmpty()) address = set.ServiceAddress;
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
                    star.Service?.RegisterAsync(serviceName, address, tag, health).Wait();
                }
                catch (Exception ex)
                {
                    XTrace.WriteException(ex);
                }
            });

            // 停止的时候移除服务
            lifetime.ApplicationStopped.Register(() =>
            {
                DefaultSpan.Current = null;

                // 从注册中心释放服务提供者和消费者
                star.Service.TryDispose();
            });

            return app;
        }
    }
}