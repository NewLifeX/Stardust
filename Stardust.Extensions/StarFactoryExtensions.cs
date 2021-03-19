using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Hosting;
using NewLife;
using NewLife.Reflection;
using Stardust;

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
            services.AddSingleton(star.Tracer);
            services.AddSingleton(star.Config);

            //services.AddHostedService<StarService>();

            services.AddSingleton(serviceProvider =>
            {
                var server = serviceProvider.GetRequiredService<IServer>();
                return server.Features.Get<IServerAddressesFeature>();
            });

            return star;
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
                if (address.IsNullOrEmpty()) address = Stardust.Setting.Current.ServiceAddress;
                if (address.IsNullOrEmpty())
                {
                    var feature = app.ServerFeatures.Get<IServerAddressesFeature>();
                    address = feature?.Addresses.Join();

                    if (address.IsNullOrEmpty())
                    {
                        if (feature != null)
                            star.Dust.Register(serviceName, () => feature?.Addresses.Join(), tag);
                        else
                            throw new Exception("尘埃客户端未能取得本地服务地址。");

                        return;
                    }
                }

                star.Dust.Register(serviceName, address, tag);
            });

            // 停止的时候移除服务
            lifetime.ApplicationStopped.Register(() =>
            {
                star.Dust.Unregister(serviceName);
            });

            return app;
        }
    }
}