extern alias web;

using System;
using Xunit;
using web::Stardust.Web.Services;

namespace ServerTest.Web;

/// <summary>调用链分享令牌测试。覆盖签发、过期、篡改与绑定追踪编号</summary>
public class TraceShareTokenTests
{
    private const String Secret = "unit-test-secret";
    private const String TraceId = "4bf92f3577b34da6a3ce929d0e0e4736";

    [Fact(DisplayName = "签发的令牌在有效期内校验通过")]
    public void Roundtrip()
    {
        var now = DateTime.UtcNow;
        var token = TraceShareTokenService.CreateToken(TraceId, Secret, 60, now);

        Assert.False(String.IsNullOrEmpty(token));
        Assert.True(TraceShareTokenService.CheckToken(TraceId, token, Secret, now));
        Assert.True(TraceShareTokenService.CheckToken(TraceId, token, Secret, now.AddMinutes(59)));
    }

    [Fact(DisplayName = "令牌到期后失效")]
    public void Expired()
    {
        var now = DateTime.UtcNow;
        var token = TraceShareTokenService.CreateToken(TraceId, Secret, 60, now);

        Assert.False(TraceShareTokenService.CheckToken(TraceId, token, Secret, now.AddMinutes(61)));
    }

    [Fact(DisplayName = "令牌不能用于其它追踪编号")]
    public void WrongTraceId()
    {
        var now = DateTime.UtcNow;
        var token = TraceShareTokenService.CreateToken(TraceId, Secret, 60, now);

        Assert.False(TraceShareTokenService.CheckToken("00000000000000000000000000000000", token, Secret, now));
    }

    [Fact(DisplayName = "签名被篡改后失效")]
    public void Tampered()
    {
        var now = DateTime.UtcNow;
        var token = TraceShareTokenService.CreateToken(TraceId, Secret, 60, now);

        var last = token[^1];
        var tampered = token[..^1] + (last == 'A' ? 'B' : 'A');
        Assert.False(TraceShareTokenService.CheckToken(TraceId, tampered, Secret, now));

        Assert.False(TraceShareTokenService.CheckToken(TraceId, token, "another-secret", now));
    }

    [Fact(DisplayName = "长期令牌不受时间影响")]
    public void Permanent()
    {
        var now = DateTime.UtcNow;
        var token = TraceShareTokenService.CreateToken(TraceId, Secret, 0, now);

        Assert.True(TraceShareTokenService.CheckToken(TraceId, token, Secret, now.AddYears(10)));
        Assert.Null(TraceShareTokenService.GetExpire(token));
    }

    [Fact(DisplayName = "限时令牌可解析出过期时间")]
    public void GetExpire_ReturnsExpiry()
    {
        var now = DateTime.UtcNow;
        var token = TraceShareTokenService.CreateToken(TraceId, Secret, 60, now);

        var expire = TraceShareTokenService.GetExpire(token);
        Assert.NotNull(expire);
        Assert.True(Math.Abs((expire.Value - now.ToLocalTime().AddMinutes(60)).TotalSeconds) < 5);
    }

    [Theory(DisplayName = "非法令牌一律不通过")]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData(".sig")]
    [InlineData("0.")]
    [InlineData("x.y")]
    public void InvalidToken(String token)
    {
        Assert.False(TraceShareTokenService.CheckToken(TraceId, token, Secret, DateTime.UtcNow));
    }
}
