using System;
using System.ComponentModel;
using Stardust;
using Xunit;

namespace Stardust.Tests;

/// <summary>客户端标识修正测试。网络未就绪时 Runtime.ClientId 缺少主机段</summary>
public class StarHelperClientIdTests
{
    [Fact]
    [DisplayName("缺少主机段的客户端标识用机器名补足")]
    public void FixClientId_FillsMachineName()
    {
        var rs = StarHelper.FixClientId("@1234");

        Assert.Equal($"{Environment.MachineName}@1234", rs);
    }

    [Fact]
    [DisplayName("已有主机段的客户端标识保持不变")]
    public void FixClientId_KeepsHostedId()
    {
        Assert.Equal("10.0.0.8@1234", StarHelper.FixClientId("10.0.0.8@1234"));
        Assert.Equal("myhost@1234", StarHelper.FixClientId("myhost@1234"));
    }

    [Fact]
    [DisplayName("空值原样返回")]
    public void FixClientId_NullOrEmpty_ReturnsAsIs()
    {
        Assert.Null(StarHelper.FixClientId(null));
        Assert.Equal("", StarHelper.FixClientId(""));
    }
}
