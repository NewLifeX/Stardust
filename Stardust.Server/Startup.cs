using System;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NewLife;
using NewLife.Caching;
using NewLife.Log;
using Stardust.Data;
using Stardust.Server.Common;
using Stardust.Server.Services;
using XCode.DataAccessLayer;

namespace Stardust.Server
{
    public class Startup
    {
        public Startup(IConfiguration configuration) => Configuration = configuration;

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            var star = new StarFactory(null, "StarServer", null);
            if (star.Server.IsNullOrEmpty()) star.Server = "http://127.0.0.1:6600";

            var tracer = star.Tracer;
            services.AddSingleton<ITracer>(tracer);

            // 默认连接字符串，如果配置文件没有设置，则采用该值
            DAL.ConnStrs.TryAdd("ConfigCenter", "MapTo=Stardust");
            DAL.ConnStrs.TryAdd("Monitor", "MapTo=Stardust");
            DAL.ConnStrs.TryAdd("MonitorLog", "MapTo=Stardust");
            DAL.ConnStrs.TryAdd("Node", "MapTo=Stardust");
            DAL.ConnStrs.TryAdd("NodeLog", "MapTo=Stardust");

            // 调整应用表名
            FixAppTableName();

            var cache = MemoryCache.Default;
            services.AddSingleton(cache);

            var set = Stardust.Server.Setting.Current;

            // 统计服务
            var traceService = new TraceStatService(tracer) { FlowPeriod = set.MonitorFlowPeriod, BatchPeriod = set.MonitorBatchPeriod };
            services.AddSingleton<ITraceStatService>(traceService);
            var appStatService = new AppDayStatService(tracer) { BatchPeriod = set.MonitorBatchPeriod };
            services.AddSingleton<IAppDayStatService>(appStatService);
            var alarmService = new AlarmService(tracer) { Period = set.AlarmPeriod };
            services.AddSingleton<IAlarmService>(alarmService);

            services.AddSingleton<TokenService>();
            services.AddSingleton<ConfigService>();

            services.AddHttpClient();

            services.AddCors(options => options.AddPolicy("star_cors", builder =>
            {
                builder.AllowAnyOrigin().AllowAnyHeader();
            }));

            // 后台服务。数据保留，定时删除过期数据
            services.AddHostedService<DataRetentionService>();
            services.AddHostedService<RedisService>();
            services.AddHostedService<NodeOnlineService>();
            services.AddHostedService<ApolloService>();
            services.AddHostedService<ShardTableService>();

            services.AddControllers()
                .AddJsonOptions(options =>
                {
                    options.JsonSerializerOptions.Converters.Add(new DateTimeConverter());
                    options.JsonSerializerOptions.Converters.Add(new JsonConverter<ISpanBuilder, DefaultSpanBuilder>());
                    options.JsonSerializerOptions.Converters.Add(new JsonConverter<ISpan, DefaultSpan>());
                    options.JsonSerializerOptions.Encoder = JavaScriptEncoder.Create(UnicodeRanges.All);
                });

            //services.Configure<KestrelServerOptions>(options =>
            //{
            //    options.AllowSynchronousIO = true;
            //});
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            var set = Stardust.Setting.Current;

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseCors("star_cors");

            //app.UseMiddleware<TracerMiddleware>();
            app.UseStardust();

            app.UseWebSockets(new WebSocketOptions()
            {
                KeepAliveInterval = TimeSpan.FromSeconds(60),
            });
            //app.UseMiddleware<NodeSocketMiddleware>();

            app.UseRouting();

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });

            // 异步初始化
            Task.Run(InitAsync);
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
            //var set2 = XCode.Setting.Current;
            //if (set2.IsNew)
            //{
            //    set2.Migration = Migration.ReadOnly;
            //    set2.Save();
            //}

            // 初始化数据库
            //var n = App.Meta.Count;
            //AppStat.Meta.Session.Dal.Db.ShowSQL = false;

            var dal = App.Meta.Session.Dal;
            dal.CheckTables();
        }

        private static void FixAppTableName()
        {
            var dal = DAL.Create("Stardust");
            var tables = dal.Tables;
            if (!tables.Any(e => e.TableName.EqualIgnoreCase("StarApp")))
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
    }
}