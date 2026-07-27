using Stardust.Data.Platform;
using Xunit;

namespace ServerTest.Mcp;

/// <summary>MCP令牌单元测试。覆盖Token生成、恒定时间比较、有效性校验</summary>
public class McpTokenTests
{
    [Fact]
    public void GenerateToken_HasSdmcpPrefix()
    {
        var token = McpToken.GenerateToken();

        Assert.StartsWith("sdmcp_", token);
    }

    [Fact]
    public void GenerateToken_HasCorrectLength()
    {
        // sdmcp_ (6) + 32 chars = 38
        var token = McpToken.GenerateToken();

        Assert.Equal(38, token.Length);
    }

    [Fact]
    public void GenerateToken_ReturnsDifferentValues()
    {
        var t1 = McpToken.GenerateToken();
        var t2 = McpToken.GenerateToken();

        Assert.NotEqual(t1, t2);
    }

    [Theory]
    [InlineData("sdmcp_abc", "sdmcp_abc", true)]
    [InlineData("sdmcp_abc", "sdmcp_xyz", false)]
    [InlineData("sdmcp_abc", "sdmcp_ab", false)]        // 不同长度
    [InlineData("", "", true)]                            // 空字符串相等
    [InlineData(null, "sdmcp_abc", false)]               // null
    [InlineData("sdmcp_abc", null, false)]               // null
    [InlineData(null, null, false)]                       // 双null
    public void SafeEquals_VariousInputs(String a, String b, Boolean expected)
    {
        var result = McpToken.SafeEquals(a, b);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void SafeEquals_ConstantTimeForEqualLength()
    {
        // 同长度不同内容应返回 false
        var a = "sdmcp_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        var b = "sdmcp_bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

        Assert.False(McpToken.SafeEquals(a, b));
        Assert.True(McpToken.SafeEquals(a, a));
    }

    [Fact]
    public void IsValid_Disabled_ReturnsFalse()
    {
        var token = new McpToken { Enable = false, ExpireTime = DateTime.MinValue };

        Assert.False(token.IsValid());
    }

    [Fact]
    public void IsValid_EnabledNoExpiry_ReturnsTrue()
    {
        var token = new McpToken { Enable = true, ExpireTime = DateTime.MinValue };

        Assert.True(token.IsValid());
    }

    [Fact]
    public void IsValid_EnabledExpired_ReturnsFalse()
    {
        var token = new McpToken { Enable = true, ExpireTime = DateTime.Now.AddDays(-1) };

        Assert.False(token.IsValid());
    }

    [Fact]
    public void IsValid_EnabledFutureExpiry_ReturnsTrue()
    {
        var token = new McpToken { Enable = true, ExpireTime = DateTime.Now.AddDays(1) };

        Assert.True(token.IsValid());
    }

    [Fact]
    public void FindByToken_EmptyOrNull_ReturnsNull()
    {
        Assert.Null(McpToken.FindByToken(""));
        Assert.Null(McpToken.FindByToken(null));
    }

    [Fact]
    public void FindById_Negative_ReturnsNull()
    {
        Assert.Null(McpToken.FindById(-1));
    }
}
