using System;
using System.Reflection;
using NewLife.Remoting;
using Stardust.Server;
using Stardust.Server.Services;
using Xunit;

namespace ServerTest.Services;

/// <summary>上行链路服务单元测试。覆盖未配置上游时不上报，以及客户端按地址缓存与重建</summary>
public class UplinkServiceTests
{
    /// <summary>通过反射调用私有的 GetClient 方法，验证客户端创建与缓存策略</summary>
    private static ApiHttpClient GetClient(UplinkService service)
    {
        var method = typeof(UplinkService).GetMethod("GetClient", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(method);

        return method.Invoke(service, null) as ApiHttpClient;
    }

    [Fact(DisplayName = "未配置上游地址时不创建客户端")]
    public void GetClient_NoServer_ReturnsNull()
    {
        var old = StarServerSetting.Current.UplinkServer;
        try
        {
            StarServerSetting.Current.UplinkServer = "";

            var service = new UplinkService();

            Assert.Null(GetClient(service));
        }
        finally
        {
            StarServerSetting.Current.UplinkServer = old;
        }
    }

    [Fact(DisplayName = "未配置上游地址时上报空模型不抛异常")]
    public void Report_NoServer_DoesNothing()
    {
        var old = StarServerSetting.Current.UplinkServer;
        try
        {
            StarServerSetting.Current.UplinkServer = "";

            var service = new UplinkService();
            service.Report(null, null);

            var model = new Stardust.Monitors.TraceModel { AppId = "test-app" };
            service.Report(null, model);

            // 未配置上游地址时不应创建客户端
            Assert.Null(GetClient(service));
        }
        finally
        {
            StarServerSetting.Current.UplinkServer = old;
        }
    }

    [Fact(DisplayName = "地址不变时复用客户端，地址变化时重建")]
    public void GetClient_ServerChanged_Rebuilds()
    {
        var old = StarServerSetting.Current.UplinkServer;
        try
        {
            var service = new UplinkService { Server = "http://127.0.0.1:1" };

            var client1 = GetClient(service);
            Assert.NotNull(client1);

            var client2 = GetClient(service);
            Assert.Same(client1, client2);

            service.Server = "http://127.0.0.1:2";
            var client3 = GetClient(service);
            Assert.NotNull(client3);
            Assert.NotSame(client1, client3);

            service.Server = "";
            StarServerSetting.Current.UplinkServer = "";
            Assert.Null(GetClient(service));
        }
        finally
        {
            StarServerSetting.Current.UplinkServer = old;
        }
    }
}
