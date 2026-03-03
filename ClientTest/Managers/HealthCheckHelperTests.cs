using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Stardust.Managers;
using Xunit;

namespace ClientTest.Managers;

public class HealthCheckHelperTests
{
    #region 空/null 入参
    [Fact]
    [Trait("Category", "HealthCheckHelper")]
    public void CheckNullReturnsTrue()
    {
        var result = HealthCheckHelper.Check(null);
        Assert.True(result);
    }

    [Fact]
    [Trait("Category", "HealthCheckHelper")]
    public void CheckEmptyStringReturnsTrue()
    {
        var result = HealthCheckHelper.Check("");
        Assert.True(result);
    }

    [Fact]
    [Trait("Category", "HealthCheckHelper")]
    public void CheckWhitespaceReturnsFalse()
    {
        var result = HealthCheckHelper.Check("   ");
        Assert.False(result);
    }
    #endregion

    #region 不支持的协议
    [Fact]
    [Trait("Category", "HealthCheckHelper")]
    public void CheckUnsupportedProtocolReturnsFalse()
    {
        var result = HealthCheckHelper.Check("ftp://localhost/health");
        Assert.False(result);
    }

    [Fact]
    [Trait("Category", "HealthCheckHelper")]
    public void CheckPlainTextAddressReturnsFalse()
    {
        var result = HealthCheckHelper.Check("localhost:8080");
        Assert.False(result);
    }
    #endregion

    #region TCP 健康检查
    [Fact]
    [Trait("Category", "HealthCheckHelper")]
    public void CheckTcpConnectsToLocalListener()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        try
        {
            var result = HealthCheckHelper.Check($"tcp://127.0.0.1:{port}", 3000);
            Assert.True(result);
        }
        finally
        {
            listener.Stop();
        }
    }

    [Fact]
    [Trait("Category", "HealthCheckHelper")]
    public void CheckTcpFailsWhenNoListener()
    {
        var result = HealthCheckHelper.Check("tcp://127.0.0.1:19999", 500);
        Assert.False(result);
    }
    #endregion

    #region HTTP 健康检查
    [Fact]
    [Trait("Category", "HealthCheckHelper")]
    public void CheckHttpFailsWhenNoServer()
    {
        var result = HealthCheckHelper.Check("http://127.0.0.1:29999/health", 500);
        Assert.False(result);
    }

    [Fact]
    [Trait("Category", "HealthCheckHelper")]
    public void CheckHttpsFailsWhenNoServer()
    {
        var result = HealthCheckHelper.Check("https://127.0.0.1:29998/health", 500);
        Assert.False(result);
    }
    #endregion

    #region UDP 健康检查
    [Fact]
    [Trait("Category", "HealthCheckHelper")]
    public void CheckUdpFailsWhenNoResponder()
    {
        var result = HealthCheckHelper.Check("udp://127.0.0.1:29997", 300);
        Assert.False(result);
    }
    #endregion

    #region 异步版本
    [Fact]
    [Trait("Category", "HealthCheckHelper")]
    public async Task CheckAsyncNullReturnsTrue()
    {
        var result = await HealthCheckHelper.CheckAsync(null);
        Assert.True(result);
    }

    [Fact]
    [Trait("Category", "HealthCheckHelper")]
    public async Task CheckAsyncEmptyReturnsTrue()
    {
        var result = await HealthCheckHelper.CheckAsync("");
        Assert.True(result);
    }

    [Fact]
    [Trait("Category", "HealthCheckHelper")]
    public async Task CheckAsyncUnsupportedProtocolReturnsFalse()
    {
        var result = await HealthCheckHelper.CheckAsync("ftp://localhost/health");
        Assert.False(result);
    }

    [Fact]
    [Trait("Category", "HealthCheckHelper")]
    public async Task CheckAsyncTcpConnectsToLocalListener()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        try
        {
            var result = await HealthCheckHelper.CheckAsync($"tcp://127.0.0.1:{port}", 3000);
            Assert.True(result);
        }
        finally
        {
            listener.Stop();
        }
    }
    #endregion
}
