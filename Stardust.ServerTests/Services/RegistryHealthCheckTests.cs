using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using NewLife;
using NewLife.Log;
using NewLife.Security;
using Stardust.Data;
using Stardust.Server.Services;
using Xunit;

namespace ServerTest.Services;

/// <summary>服务健康检查测试。覆盖探测成功回写健康状态与探测失败的降级记录（REG-5）</summary>
public class RegistryHealthCheckTests
{
    /// <summary>创建只注入跟踪器的注册服务实例。HealthCheck 只用到 _tracer，其余依赖无需初始化</summary>
    private static RegistryService CreateService()
    {
        var svc = (RegistryService)RuntimeHelpers.GetUninitializedObject(typeof(RegistryService));
        typeof(RegistryService).GetField("_tracer", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(svc, DefaultTracer.Instance);
        return svc;
    }

    /// <summary>取一个空闲端口</summary>
    private static Int32 GetFreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static (Service service, AppService appService) CreateAppService(String healthAddress, String address)
    {
        var service = new Service
        {
            Name = "svc_" + Rand.NextString(8),
            HealthAddress = healthAddress,
            Enable = true,
        };
        service.Insert();

        var appService = new AppService
        {
            ServiceId = service.Id,
            ServiceName = service.Name,
            Address = address,
            Enable = true,
        };
        appService.Insert();

        return (service, appService);
    }

    [Fact(DisplayName = "健康检查成功时回写健康状态与响应内容")]
    public async Task HealthCheck_Success_SetsHealthy()
    {
        var port = GetFreePort();
        var prefix = $"http://127.0.0.1:{port}/";
        var listener = new HttpListener();
        listener.Prefixes.Add(prefix);
        listener.Start();

        var (service, appService) = CreateAppService("/health", $"http://127.0.0.1:{port}");
        try
        {
            _ = Task.Run(async () =>
            {
                var ctx = await listener.GetContextAsync();
                var body = Encoding.UTF8.GetBytes("ok");
                ctx.Response.StatusCode = 200;
                ctx.Response.ContentType = "text/plain";
                await ctx.Response.OutputStream.WriteAsync(body, 0, body.Length);
                ctx.Response.Close();
            });

            await CreateService().HealthCheck(appService);

            Assert.True(appService.Healthy);
            Assert.Equal("ok", appService.CheckResult);
            Assert.Equal(1, appService.CheckTimes);
            Assert.True(appService.LastCheck.Year > 2000);

            // 已落库
            var saved = AppService.FindById(appService.Id);
            Assert.NotNull(saved);
            Assert.True(saved.Healthy);
        }
        finally
        {
            listener.Stop();
            appService.Delete();
            service.Delete();
        }
    }

    [Fact(DisplayName = "目标不可达时记为不健康并保留异常信息")]
    public async Task HealthCheck_Unreachable_MarksUnhealthy()
    {
        var port = GetFreePort();
        var (service, appService) = CreateAppService("/health", $"http://127.0.0.1:{port}");
        try
        {
            await CreateService().HealthCheck(appService);

            Assert.False(appService.Healthy);
            Assert.False(appService.CheckResult.IsNullOrEmpty());
            Assert.Equal(1, appService.CheckTimes);
        }
        finally
        {
            appService.Delete();
            service.Delete();
        }
    }

    [Fact(DisplayName = "未配置健康检测地址时直接跳过")]
    public async Task HealthCheck_NoAddress_Skip()
    {
        var (service, appService) = CreateAppService(null, "http://127.0.0.1:1");
        try
        {
            await CreateService().HealthCheck(appService);

            // 不探测、不回写
            Assert.Equal(0, appService.CheckTimes);
        }
        finally
        {
            appService.Delete();
            service.Delete();
        }
    }
}
