using Stardust.Data;
using Xunit;

namespace Stardust.Tests;

/// <summary>审计历史脱敏测试</summary>
public class HistorySanitizerTests
{
    [Fact(DisplayName = "历史备注中的JSON密钥字段被掩码")]
    public void Redact_JsonSecret()
    {
        var remark = "{\"Code\":\"StarServer\",\"Secret\":\"$md5$1793198435$93FCA6D1E7CF4C414E776F6B785EFCE6\",\"ClientId\":\"10.0.0.1@1234\"}";

        var rs = HistorySanitizer.Redact(remark);

        Assert.DoesNotContain("93FCA6D1", rs);
        Assert.Contains("\"Secret\":\"***\"", rs);
        Assert.Contains("\"ClientId\":\"10.0.0.1@1234\"", rs);
    }

    [Fact(DisplayName = "历史备注中的查询串密钥被掩码")]
    public void Redact_QueryStringSecret()
    {
        var rs = HistorySanitizer.Redact("login?code=node01&secret=abc123&clientId=10.0.0.1@1234");

        Assert.DoesNotContain("abc123", rs);
        Assert.Contains("secret=***", rs);
        Assert.Contains("code=node01", rs);
    }

    [Fact(DisplayName = "历史备注中的密码与令牌字段被掩码")]
    public void Redact_PasswordAndToken()
    {
        var rs = HistorySanitizer.Redact("{\"password\":\"p@ss\",\"access_token\":\"tok\",\"appSecret\":\"sec\"}");

        Assert.DoesNotContain("p@ss", rs);
        Assert.DoesNotContain("\"tok\"", rs);
        Assert.DoesNotContain("\"sec\"", rs);
        Assert.Contains("\"password\":\"***\"", rs);
        Assert.Contains("\"access_token\":\"***\"", rs);
        Assert.Contains("\"appSecret\":\"***\"", rs);
    }

    [Fact(DisplayName = "普通历史备注保持不变")]
    public void Redact_PlainTextUnchanged()
    {
        const String remark = "Secret validation failed. [node01/10.0.0.1] login at 2026-11-14 20:00:00";

        Assert.Equal(remark, HistorySanitizer.Redact(remark));
    }

    [Fact(DisplayName = "空备注原样返回")]
    public void Redact_NullOrEmpty()
    {
        Assert.Null(HistorySanitizer.Redact(null));
        Assert.Equal("", HistorySanitizer.Redact(""));
    }
}
