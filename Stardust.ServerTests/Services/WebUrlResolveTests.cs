using System;
using Stardust.Server;
using Xunit;

namespace ServerTest.Services;

/// <summary>控制台地址解析测试。确保已配置的地址不被注册中心解析结果覆盖（SVR-2）</summary>
public class WebUrlResolveTests
{
    [Fact(DisplayName = "已配置控制台地址时不再改写，避免每次启动配置抖动")]
    public void PickWebUrl_KeepsConfiguredValue()
    {
        var current = "https://stardust.example.com/";

        // 注册中心每次都返回历史实例与临时地址，顺序还不固定
        Assert.Null(Startup.PickWebUrl(current, ["http://10.0.0.3:6680", "http://127.0.0.1:16680"]));
        Assert.Null(Startup.PickWebUrl(current, []));
        Assert.Null(Startup.PickWebUrl(current, null));
    }

    [Fact(DisplayName = "未配置控制台地址时采用注册中心解析结果")]
    public void PickWebUrl_UsesResolvedAddresses()
    {
        Assert.Equal("http://10.0.0.3:6680;http://192.168.96.1:6680", Startup.PickWebUrl("", ["http://10.0.0.3:6680", "http://192.168.96.1:6680"]));
        Assert.Equal("http://10.0.0.3:6680", Startup.PickWebUrl(null, ["http://10.0.0.3:6680"]));
    }

    [Fact(DisplayName = "未配置且注册中心无地址时保持空值，不写入空串")]
    public void PickWebUrl_NoAddresses_ReturnsNull()
    {
        Assert.Null(Startup.PickWebUrl("", []));
        Assert.Null(Startup.PickWebUrl(null, null));
    }
}
