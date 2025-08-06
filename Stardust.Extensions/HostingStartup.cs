using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using NewLife.Log;

// 通过程序集特性，将此类标记为宿主启动项
[assembly: HostingStartup(typeof(Stardust.Extensions.HostingStartup))]

namespace Stardust.Extensions;

/// <summary>Web主机启动</summary>
public class HostingStartup : IHostingStartup
{
    /// <summary>配置服务</summary>
    /// <param name="builder"></param>
    public void Configure(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.AddStardust();

            services.AddTransient<IStartupFilter, StartupFilter>();
        });

        //builder.Configure(app =>
        //{
        //    XTrace.WriteLine("HostingStartup injected Stardust");
        //    app.UseStardust();
        //});
    }
}

internal class StartupFilter : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        return app =>
        {
            XTrace.WriteLine("StartupFilter injected Stardust");
            app.UseStardust();

            next(app);
        };
    }
}