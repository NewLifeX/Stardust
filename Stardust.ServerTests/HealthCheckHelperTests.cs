using System;
using System.Threading.Tasks;
using NewLife.Log;
using Stardust.Managers;
using Xunit;

namespace Stardust.ServerTests;

/// <summary>健康检查助手测试</summary>
public class HealthCheckHelperTests
{
    /// <summary>测试HTTP健康检查（失败场景）</summary>
    [Fact]
    public void TestHttpHealthCheckFail()
    {
        // 测试不存在的地址
        var result = HealthCheckHelper.Check("http://localhost:19999/notexist", 1000);
        Assert.False(result);
    }

    /// <summary>测试TCP健康检查（失败场景）</summary>
    [Fact]
    public void TestTcpHealthCheckFail()
    {
        // 测试不存在的端口
        var result = HealthCheckHelper.Check("tcp://localhost:19999", 1000);
        Assert.False(result);
    }

    /// <summary>测试UDP健康检查（失败场景）</summary>
    [Fact]
    public void TestUdpHealthCheckFail()
    {
        // 测试不存在的端口（UDP会超时）
        var result = HealthCheckHelper.Check("udp://localhost:19999", 1000);
        Assert.False(result);
    }

    /// <summary>测试不支持的协议</summary>
    [Fact]
    public void TestUnsupportedProtocol()
    {
        var result = HealthCheckHelper.Check("ftp://localhost:21", 1000);
        Assert.False(result);
    }

    /// <summary>测试空地址（应该返回true）</summary>
    [Fact]
    public void TestEmptyAddress()
    {
        var result = HealthCheckHelper.Check("", 1000);
        Assert.True(result);
        
        result = HealthCheckHelper.Check(null, 1000);
        Assert.True(result);
    }

    /// <summary>测试HTTP健康检查异步（失败场景）</summary>
    [Fact]
    public async Task TestHttpHealthCheckAsyncFail()
    {
        var result = await HealthCheckHelper.CheckAsync("http://localhost:19999/notexist", 1000);
        Assert.False(result);
    }

    /// <summary>测试TCP健康检查异步（失败场景）</summary>
    [Fact]
    public async Task TestTcpHealthCheckAsyncFail()
    {
        var result = await HealthCheckHelper.CheckAsync("tcp://localhost:19999", 1000);
        Assert.False(result);
    }
}
