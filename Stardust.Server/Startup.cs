using System.Text;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NewLife.Cube.WebMiddleware;
using NewLife.Log;
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

            var tracer = star.Tracer;
            services.AddSingleton<ITracer>(tracer);

            // 默认连接字符串，如果配置文件没有设置，则采用该值
            DAL.ConnStrs.TryAdd("ConfigCenter", "MapTo=Stardust");
            DAL.ConnStrs.TryAdd("Monitor", "MapTo=Stardust");
            DAL.ConnStrs.TryAdd("MonitorLog", "MapTo=Stardust");
            DAL.ConnStrs.TryAdd("Node", "MapTo=Stardust");
            DAL.ConnStrs.TryAdd("NodeLog", "MapTo=Stardust");

            var set = Stardust.Server.Setting.Current;

            // 统计服务
            var traceService = new TraceStatService(tracer) { FlowPeriod = set.MonitorFlowPeriod, BatchPeriod = set.MonitorBatchPeriod };
            services.AddSingleton<ITraceStatService>(traceService);
            var appStatService = new AppDayStatService(tracer) { BatchPeriod = set.MonitorBatchPeriod };
            services.AddSingleton<IAppDayStatService>(appStatService);
            var alarmService = new AlarmService(tracer) { Period = set.AlarmPeriod };
            services.AddSingleton<IAlarmService>(alarmService);

            services.AddSingleton<AppService>();
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

            services.AddControllers()
                .AddJsonOptions(options =>
                {
                    options.JsonSerializerOptions.Converters.Add(new DateTimeConverter());
                    options.JsonSerializerOptions.Converters.Add(new JsonConverter<ISpanBuilder, DefaultSpanBuilder>());
                    options.JsonSerializerOptions.Converters.Add(new JsonConverter<ISpan, DefaultSpan>());
                    options.JsonSerializerOptions.Encoder = JavaScriptEncoder.Create(UnicodeRanges.All);
                });

            services.Configure<KestrelServerOptions>(options =>
            {
                options.AllowSynchronousIO = true;
            });
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            var set = Stardust.Setting.Current;

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseCors("star_cors");

            //app.UseHttpsRedirection();
            app.UseMiddleware<TracerMiddleware>();

            app.UseRouting();

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }
}