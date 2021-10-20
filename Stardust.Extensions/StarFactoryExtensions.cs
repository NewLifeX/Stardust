using System;
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

        /// <summary>使用星尘，注入跟踪中间件</summary>
        /// <param name="app"></param>
        /// <returns></returns>
        public static IApplicationBuilder UseStardust(this IApplicationBuilder app)
        {
            var provider = app.ApplicationServices;
            var tracer = provider.GetRequiredService<ITracer>();

            if (TracerMiddleware.Tracer == null) TracerMiddleware.Tracer = tracer;
            if (TracerMiddleware.Tracer != null) app.UseMiddleware<TracerMiddleware>();

            return app;
        }

        /// <summary>发布服务到注册中心</summary>
        /// <param name="app"></param>
        /// <param name="serviceName">服务名</param>
        /// <param name="address">服务地址</param>
        /// <param name="tag">特性标签</param>
        /// <returns></returns>
        public static IApplicationBuilder RegisterService(this IApplicationBuilder app, String serviceName, String address = null, String tag = null)
        {
            var star = app.ApplicationServices.GetRequiredService<StarFactory>();
            if (star == null) throw new InvalidOperationException("未注册StarFactory，需要AddStardust注册。");

            if (serviceName.IsNullOrEmpty()) serviceName = AssemblyX.Entry.Name;

            // 启动的时候注册服务
            var lifetime = app.ApplicationServices.GetRequiredService<IHostApplicationLifetime>();
            lifetime.ApplicationStarted.Register(() =>
            {
                try
                {
                    if (address.IsNullOrEmpty()) address = Stardust.Setting.Current.ServiceAddress;
                    if (address.IsNullOrEmpty())
                    {
                        var feature = app.ServerFeatures.Get<IServerAddressesFeature>();
                        address = feature?.Addresses.Join();

                        if (address.IsNullOrEmpty())
                        {
                            if (feature == null) throw new Exception("尘埃客户端未能取得本地服务地址。");

                            star.Service.Register(serviceName, () => feature?.Addresses.Join(), tag);

                            return;
                        }
                    }

                    star.Service.RegisterAsync(serviceName, address, tag).Wait();
                }
                catch (Exception ex)
                {
                    XTrace.WriteException(ex);
                }
            });

            // 停止的时候移除服务
            lifetime.ApplicationStopped.Register(() =>
            {
                try
                {
                    star.Service.Unregister(serviceName);
                }
                catch (Exception ex)
                {
                    XTrace.WriteException(ex);
                }
            });

            return app;
        }
    }
}