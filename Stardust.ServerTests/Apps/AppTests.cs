using System;
using Stardust.Data;
using Xunit;

namespace Stardust.ServerTests.Apps;

public class AppTests
{
    [Theory]
    [InlineData("10.*,192.168.1.*", "192.168.0.*", "10.0.0.6", "匹配白名单且未匹配黑名单")]
    [InlineData("10.*;192.168.1.*", "", "10.0.0.6", "匹配白名单")]
    [InlineData("", "192.168.0.*", "10.0.0.6", "匹配白名单")]
    [InlineData("", "", "10.0.0.6", "没有黑白名单")]
    [InlineData("10.*,192.168.1.*", "192.168.0.*", "192.168.1.6", "匹配白名单")]
    [InlineData("10.*;192.168.1.*", "", "192.168.1.6", "匹配白名单")]
    [InlineData("", "192.168.0.*", "192.168.1.6", "匹配白名单")]
    [InlineData("", "", "192.168.1.6", "没有黑白名单")]
    public void MatchSuccess(String whites, String blacks, String ip, String message)
    {
        var app = new App
        {
            WhiteIPs = whites,
            BlackIPs = blacks,
        };

        var rs = app.MatchIp(ip);
        Assert.True(rs, message);
    }

    [Theory]
    [InlineData("10.*,192.168.1.*", "192.168.0.*", "192.168.0.6", "匹配黑名单")]
    [InlineData("10.*;192.168.1.*", "", "192.168.0.6", "不在白名单里面")]
    [InlineData("", "192.168.0.*", "192.168.0.6", "匹配黑名单")]
    [InlineData("10.*,192.168.1.*", "192.168.*", "192.168.0.6", "同时匹配黑白名单")]
    public void MatchFail(String whites, String blacks, String ip, String message)
    {
        var app = new App
        {
            WhiteIPs = whites,
            BlackIPs = blacks,
        };

        var rs = app.MatchIp(ip);
        Assert.False(rs, message);
    }
}
