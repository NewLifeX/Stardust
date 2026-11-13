using System;
using System.ComponentModel;
using System.IO;
using NewLife;
using Stardust.Managers;
using Xunit;

namespace Stardust.Tests;

/// <summary>本机 RPC 共享令牌测试</summary>
public class LocalApiTokenTests
{
    [Fact]
    [DisplayName("令牌校验：一致通过，长度不同或空值拒绝")]
    public void Verify_MatchesOnlyIdenticalToken()
    {
        Assert.True(LocalApiToken.Verify("abcdef", "abcdef"));
        Assert.False(LocalApiToken.Verify("abcdef", "abcdeg"));
        Assert.False(LocalApiToken.Verify("abc", "abcdef"));
        Assert.False(LocalApiToken.Verify(null, "abcdef"));
        Assert.False(LocalApiToken.Verify("abcdef", null));
        Assert.False(LocalApiToken.Verify("", ""));
    }

    [Fact]
    [DisplayName("令牌校验：忽略调用方两侧空白")]
    public void Verify_TrimsInput()
    {
        Assert.True(LocalApiToken.Verify(" abcdef ", "abcdef"));
    }

    [Fact]
    [DisplayName("创建令牌：重复调用沿用同一令牌，且可从磁盘读回")]
    public void Ensure_IsStableAndReadable()
    {
        var token = LocalApiToken.Ensure();
        Assert.False(token.IsNullOrEmpty());

        var token2 = LocalApiToken.Ensure();
        Assert.Equal(token, token2);

        var file = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, LocalApiToken.FileName);
        Assert.True(File.Exists(file));
        Assert.Equal(token, File.ReadAllText(file).Trim());
    }
}
