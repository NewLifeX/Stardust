using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NewLife;
using NewLife.Cube;
using NewLife.Cube.WebMiddleware;
using NewLife.Log;
using NewLife.Remoting;
using Stardust.Monitors;
using Stardust.Server.Services;
using Stardust.Web.Areas.Monitors.Controllers;
using XCode.DataAccessLayer;

namespace Stardust.Web
{
    public class Startup
    {
        public Startup(IConfiguration configuration) => Configuration = configuration;

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            var tracer = StarTracer.Register();
            services.AddSingleton<ITracer>(tracer);

            // 统计
            var appService = new AppDayStatService();
            services.AddSingleton<IAppDayStatService>(appService);
            var traceService = new TraceStatService();
            services.AddSingleton<ITraceStatService>(traceService);
            AppDayStatController.AppStat = appService;
            AppDayStatController.TraceStat = traceService;

            services.AddSingleton<IRedisService, RedisService>();

            services.AddSingleton<ConfigService>();

            services.AddControllersWithViews();
            services.AddCube();
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            //var set = Stardust.Setting.Current;

            // 使用Cube前添加自己的管道
            if (env.IsDevelopment())
                app.UseDeveloperExceptionPage();
            else
                app.UseExceptionHandler("/CubeHome/Error");

            //app.UseStaticFiles();

            //app.UseRouting();

            //app.UseAuthorization();

            //if (!set.Server.IsNullOrEmpty()) app.UseMiddleware<TracerMiddleware>();

            app.UseCube(env);

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllerRoute(
                    name: "default",
                    pattern: "{controller=CubeHome}/{action=Index}/{id?}");
            });
        }
    }
}