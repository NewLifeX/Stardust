using System.Text;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NewLife;
using NewLife.Cube.WebMiddleware;
using NewLife.Log;
using Stardust.Monitors;
using Stardust.Server.Common;
using Stardust.Server.Services;

namespace Stardust.Server
{
    public class Startup
    {
        public Startup(IConfiguration configuration) => Configuration = configuration;

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            var tracer = StarTracer.Register();
            services.AddSingleton<ITracer>(tracer);

            var set2 = Stardust.Server.Setting.Current;

            // 统计服务
            var traceService = new TraceStatService { FlowPeriod = set2.MonitorFlowPeriod, BatchPeriod = set2.MonitorBatchPeriod };
            services.AddSingleton<ITraceStatService>(traceService);
            var appStatService = new AppDayStatService { BatchPeriod = set2.MonitorBatchPeriod };
            services.AddSingleton<IAppDayStatService>(appStatService);
            var alarmService = new AlarmService { Period = set2.AlarmPeriod };
            services.AddSingleton<IAlarmService>(alarmService);

            services.AddSingleton<AppService>();
            services.AddSingleton<ConfigService>();

            services.AddHttpClient();

            // 后台服务。数据保留，定时删除过期数据
            services.AddHostedService<DataRetentionService>();
            services.AddHostedService<RedisService>();

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

            //app.UseHttpsRedirection();
            if (!set.Server.IsNullOrEmpty()) app.UseMiddleware<TracerMiddleware>();

            app.UseRouting();

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }
}