using System;
using NewLife;
using Stardust;
using Xunit;

namespace Stardust.Tests;

/// <summary>服务端时间鉴权测试。本机时钟偏差不应导致登录失败，也不应触发修改系统时钟</summary>
public class ServerTimeTests
{
    [Fact(DisplayName = "盐值使用服务端时间而不是本机时间")]
    public void PasswordProvider_UsesServerTimeSalt()
    {
        // 模拟本机比服务端慢 11 分钟（宿舍A4 实测偏差）
        var serverNow = new DateTime(2026, 11, 11, 0, 0, 0, DateTimeKind.Local).AddMinutes(-11);
        var provider = new ServerTimePasswordProvider(() => serverNow) { Algorithm = "md5", SaltTime = 60 };

        var parts = provider.Hash("secret").Split('$');

        Assert.Equal(4, parts.Length);
        Assert.Equal("md5", parts[1]);
        Assert.Equal(serverNow.ToUniversalTime().ToInt().ToString(), parts[2]);
    }

    [Fact(DisplayName = "关闭 SaltTime 时退回随机盐值")]
    public void PasswordProvider_SaltTimeDisabled_RandomSalt()
    {
        var provider = new ServerTimePasswordProvider(() => DateTime.Now) { Algorithm = "md5", SaltTime = 0 };

        var parts = provider.Hash("secret").Split('$');

        Assert.Equal(4, parts.Length);
        Assert.Equal(16, parts[2].Length);
    }

    [Theory(DisplayName = "解析 /api 返回的服务端时间")]
    [InlineData("{\"serverTime\":1762790400000}", 1762790400000L)]
    [InlineData("{\"ServerTime\":1762790400000}", 1762790400000L)]
    [InlineData("{\"name\":\"StarServer\",\"time\":\"2026-11-11 00:00:00\",\"serverTime\":1762790400000}", 1762790400000L)]
    [InlineData("{\"name\":\"StarServer\"}", 0L)]
    [InlineData("not-json", 0L)]
    [InlineData("", 0L)]
    public void ParseServerTime_Works(String json, Int64 expected) => Assert.Equal(expected, ServerTimeProbe.ParseServerTime(json));

    [Theory(DisplayName = "服务端地址支持逗号分隔的多个节点")]
    [InlineData("http://127.0.0.1:6600", "http://127.0.0.1:6600/api")]
    [InlineData("http://127.0.0.1:6600/", "http://127.0.0.1:6600/api")]
    [InlineData("http://a:6600,http://b:6600", "http://a:6600/api")]
    [InlineData(" http://a:6600 , http://b:6600 ", "http://a:6600/api")]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void GetTimeUrl_Works(String? server, String? expected) => Assert.Equal(expected, ServerTimeProbe.GetTimeUrl(server));
}
