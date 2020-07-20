using System.Text;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NewLife;
using NewLife.Cube.WebMiddleware;
using NewLife.Log;
using NewLife.Remoting;
using Stardust.Monitors;
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

            var set = Stardust.Server.Setting.Current;
            if (!set.TracerServer.IsNullOrEmpty())
            {
                // APM跟踪器
                var tracer = new StarTracer(set.TracerServer) { Log = XTrace.Log };
                DefaultTracer.Instance = tracer;
                ApiHelper.Tracer = tracer;
                DAL.GlobalTracer = tracer;
                TracerMiddleware.Tracer = tracer;

                services.AddSingleton<ITracer>(tracer);
            }

            // 统计服务
            var traceService = new TraceStatService();
            services.AddSingleton<ITraceStatService>(traceService);
            var appStatService = new AppDayStatService();
            services.AddSingleton<IAppDayStatService>(appStatService);

            services.AddHttpClient();

            services.AddControllers()
                .AddJsonOptions(options =>
                {
                    options.JsonSerializerOptions.Converters.Add(new DateTimeConverter());
                    options.JsonSerializerOptions.Encoder = JavaScriptEncoder.Create(UnicodeRanges.All);
                });
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            var set = Stardust.Server.Setting.Current;

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            //app.UseHttpsRedirection();
            if (!set.TracerServer.IsNullOrEmpty()) app.UseMiddleware<TracerMiddleware>();

            app.UseRouting();

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }
}