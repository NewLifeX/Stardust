using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using NewLife;
using Stardust.Server;
using Xunit;

namespace ServerTest.Controllers;

/// <summary>接口指纹/枚举收敛安全测试（安全扫描 V-13）</summary>
public class ApiSecurityTests
{
    private readonly TestServer _server;

    public ApiSecurityTests()
    {
#pragma warning disable CS0618, ASPDEPR008
        _server = new TestServer(WebHost.CreateDefaultBuilder()
            .UseStartup<Startup>());
#pragma warning restore CS0618, ASPDEPR008
    }

    private async Task<String> GetAsync(String path, String xff)
    {
        var client = _server.CreateClient();
        if (!xff.IsNullOrEmpty()) client.DefaultRequestHeaders.Add("X-Forwarded-For", xff);
        var response = await client.GetAsync(path);
        return await response.Content.ReadAsStringAsync();
    }

    [Fact(DisplayName = "公网来源访问接口列表被拒绝")]
    public async Task PublicIP_ApiAll_Rejected()
    {
        var body = await GetAsync("/api/All", "8.8.8.8");

        Assert.Contains("仅内网", body);
        Assert.Contains("\"code\"", body);
    }

    [Fact(DisplayName = "公网来源访问/api仅返回探针标识，不含OS/编译/端口/主机指纹")]
    public async Task PublicIP_Api_NoFingerprint()
    {
        var body = await GetAsync("/api", "8.8.8.8");
        var lower = body.ToLower();

        // 探针保留应用标识（name/title/version）便于多域名/反代识别，隐藏环境指纹
        Assert.True(lower.Contains("\"name\"") && lower.Contains("\"version\""), "应含探针标识: " + body);
        Assert.False(lower.Contains("\"compile\""), "不应含编译时间: " + body);
        Assert.False(lower.Contains("\"os\""), "不应含OS: " + body);
        Assert.False(lower.Contains("\"port\""), "不应含端口: " + body);
        Assert.False(lower.Contains("\"remote\""), "不应含远端地址: " + body);
        Assert.True(lower.Contains("\"time\""), "应含健康时间: " + body);
    }

    [Fact(DisplayName = "内网来源访问/api返回完整信息（回归保护）")]
    public async Task IntranetIP_Api_FullInfo()
    {
        var body = await GetAsync("/api", "192.168.1.5");
        var lower = body.ToLower();

        Assert.Contains("fileversion", lower);
    }
}
