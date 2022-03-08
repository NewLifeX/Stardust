using NewLife;
using NewLife.Cube;
using NewLife.Log;
using Stardust.Data.Configs;
using Stardust.Server.Services;
using XCode;
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
            using var span = star.Tracer?.NewSpan(nameof(ConfigureServices));

            // 启用配置中心，务必在数据库操作和生成雪花Id之前
            _ = star.Config;

            // 统计
            services.AddSingleton<IAppDayStatService, AppDayStatService>();
            services.AddSingleton<ITraceItemStatService, TraceItemStatService>();
            services.AddSingleton<ITraceStatService, TraceStatService>();

            services.AddSingleton<IRedisService, RedisService>();

            services.AddSingleton<TokenService>();
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
            var tracer = app.ApplicationServices.GetRequiredService<ITracer>();
            using var span = tracer?.NewSpan(nameof(Configure));

            // 调整应用表名
            FixAppTableName();

            EntityFactory.InitConnection("Stardust");

            TrimOldAppConfig();

            // 使用Cube前添加自己的管道
            if (env.IsDevelopment())
                app.UseDeveloperExceptionPage();
            else
                app.UseExceptionHandler("/CubeHome/Error");

            //app.UseStardust();
            app.UseCube(env);

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllerRoute(
                    name: "default",
                    pattern: "{controller=CubeHome}/{action=Index}/{id?}");
            });

            // 发布服务到星尘注册中心，需要指定服务名
            app.RegisterService("StarWeb", null, env.EnvironmentName);

            //// 从星尘注册中心消费服务，指定需要消费的服务名
            //app.ConsumeService("StarWeb");
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
            var set2 = NewLife.Cube.Setting.Current;
            if (set2.IsNew && set2.UploadPath == "Uploads")
            {
                XTrace.WriteLine("修正上传目录");
                set2.UploadPath = "../Uploads";
                set2.Save();
            }
        }

        private static void FixAppTableName()
        {
            var dal = DAL.Create("Stardust");
            var tables = dal.Tables;
            if (tables != null && !tables.Any(e => e.TableName.EqualIgnoreCase("StarApp")))
            {
                XTrace.WriteLine("未发现Star应用新表 StarApp");

                // 验证表名和部分字段名，避免误改其它表
                var dt = tables.FirstOrDefault(e => e.TableName.EqualIgnoreCase("App"));
                if (dt != null && dt.Columns.Any(e => e.ColumnName.EqualIgnoreCase("AutoActive")))
                {
                    XTrace.WriteLine("发现Star应用旧表 App ，准备重命名");

                    var rs = dal.Execute($"Alter Table App Rename To StarApp");
                    XTrace.WriteLine("重命名结果：{0}", rs);
                }
            }
        }

        private static void TrimOldAppConfig()
        {
            AppConfig.TrimAll();
        }
    }
}