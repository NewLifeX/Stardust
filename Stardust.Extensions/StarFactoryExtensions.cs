using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server.Features;
using NewLife;
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
            var star = new StarFactory(appId);

            services.AddSingleton(star);
            services.AddSingleton(star.Tracer);
            services.AddSingleton(star.Config);

            services.AddHostedService<StarService>();

            return star;
        }

        /// <summary>发布服务到注册中心</summary>
        /// <param name="app"></param>
        /// <param name="serviceName">服务名</param>
        /// <param name="tag"></param>
        /// <returns></returns>
        public static IApplicationBuilder PublishService(this IApplicationBuilder app, String serviceName, String tag = null)
        {
            var star = app.ApplicationServices.GetRequiredService<StarFactory>();
            if (star == null) throw new InvalidOperationException("未注册StarFactory，需要AddStardust注册。");

            var feature = app.ServerFeatures.Get<IServerAddressesFeature>();
            var addrs = feature?.Addresses.Join();
            if (addrs.IsNullOrEmpty()) return app;
            //XTrace.WriteLine("{0}", feature?.Addresses.Join());

            if (serviceName.IsNullOrEmpty()) serviceName = AssemblyX.Entry.Name;

            star.Dust.PublishAsync(serviceName, addrs, tag).Wait();

            return app;
        }
    }
}