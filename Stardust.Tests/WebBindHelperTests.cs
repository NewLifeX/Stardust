using System.ComponentModel;
using System.Net;
using StarAgent.WebPanel;
using Xunit;

namespace Stardust.Tests;

/// <summary>WebBindHelper 绑定地址解析测试</summary>
/// <remarks>
/// Web 面板安全默认：默认仅本机监听，* 或 Any 表示监听所有网卡，非法值回退本机环回。
/// 通过链接 StarAgent 源码直测（StarAgent 为可执行工程，不便于直接引用）。
/// </remarks>
public class WebBindHelperTests
{
    [Fact]
    [DisplayName("Parse_空值_回退本机环回")]
    public void Parse_Empty_ReturnsLoopback()
    {
        Assert.Equal(IPAddress.Loopback, WebBindHelper.Parse(null));
        Assert.Equal(IPAddress.Loopback, WebBindHelper.Parse(""));
        Assert.Equal(IPAddress.Loopback, WebBindHelper.Parse("   "));
    }

    [Fact]
    [DisplayName("Parse_星号或Any_监听所有网卡")]
    public void Parse_StarOrAny_ReturnsNull()
    {
        Assert.Null(WebBindHelper.Parse("*"));
        Assert.Null(WebBindHelper.Parse("0.0.0.0"));
        Assert.Null(WebBindHelper.Parse("::"));
    }

    [Fact]
    [DisplayName("Parse_具体IP_返回该地址")]
    public void Parse_Ip_ReturnsParsed()
    {
        Assert.Equal(IPAddress.Parse("192.168.1.10"), WebBindHelper.Parse("192.168.1.10"));
        Assert.Equal(IPAddress.IPv6Loopback, WebBindHelper.Parse("::1"));
    }

    [Fact]
    [DisplayName("Parse_非法值_回退本机环回")]
    public void Parse_Invalid_ReturnsLoopback()
    {
        Assert.Equal(IPAddress.Loopback, WebBindHelper.Parse("not-an-ip"));
        Assert.Equal(IPAddress.Loopback, WebBindHelper.Parse("192.168.1.300"));
    }
}
