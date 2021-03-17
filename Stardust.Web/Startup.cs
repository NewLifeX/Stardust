using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NewLife;
using NewLife.Cube;
using NewLife.Log;
using Stardust.Data;
using Stardust.Server.Services;
using XCode.DataAccessLayer;

namespace Stardust.Web
{
    public class Startup
    {
        public Startup(IConfiguration configuration) => Configuration = configuration;

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            var star = services.AddStardust("StarWeb");

            var tracer = star.Tracer;
            services.AddSingleton<ITracer>(tracer);

            // 默认连接字符串，如果配置文件没有设置，则采用该值
            DAL.ConnStrs.TryAdd("ConfigCenter", "MapTo=Stardust");
            DAL.ConnStrs.TryAdd("Monitor", "MapTo=Stardust");
            DAL.ConnStrs.TryAdd("MonitorLog", "MapTo=Stardust");
            DAL.ConnStrs.TryAdd("Node", "MapTo=Stardust");
            DAL.ConnStrs.TryAdd("NodeLog", "MapTo=Stardust");

            // 统计
            services.AddSingleton<IAppDayStatService, AppDayStatService>();
            services.AddSingleton<ITraceStatService, TraceStatService>();

            services.AddSingleton<IRedisService, RedisService>();

            services.AddSingleton<ConfigService>();

            // 后台服务。数据保留，定时删除过期数据
            services.AddHostedService<ApolloService>();

            // 异步初始化
            Task.Run(InitAsync);

            services.AddControllersWithViews();
            services.AddCube();
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            // 使用Cube前添加自己的管道
            if (env.IsDevelopment())
                app.UseDeveloperExceptionPage();
            else
                app.UseExceptionHandler("/CubeHome/Error");

            app.UseCube(env);

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllerRoute(
                    name: "default",
                    pattern: "{controller=CubeHome}/{action=Index}/{id?}");
            });

            //// 发布服务到星尘注册中心
            //app.PublishService("StarWeb");
        }

        private static void InitAsync()
        {
            // 配置
            var set = NewLife.Setting.Current;
            if (set.IsNew)
            {
                set.DataPath = "../Data";
                set.Save();
            }

            // 初始化数据库
            var n = App.Meta.Count;
            //AppStat.Meta.Session.Dal.Db.ShowSQL = false;
        }
    }
}