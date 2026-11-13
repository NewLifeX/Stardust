using System.ComponentModel;
using System.Net;
using NewLife;
using NewLife.Log;
using NewLife.Net;
using NewLife.Remoting;
using StarAgent;
using Stardust.Managers;
using Xunit;

namespace Stardust.Tests;

/// <summary>本机 RPC 令牌校验测试。来源地址判定挡不住同机低权限进程，需额外校验 local.token</summary>
public class StarServiceTokenTests
{
    [Fact]
    [DisplayName("SetServer_缺少或错误令牌被拒绝_正确令牌放行")]
    public async Task SetServer_RequiresLocalToken()
    {
        var token = LocalApiToken.Ensure();
        Assert.False(token.IsNullOrEmpty());

        using var svr = LocalApiServer.CreateServer(new NetUri(NetType.Udp, IPAddress.Loopback.ToString(), 0), null, Logger.Null);
        using var svc = new StarService
        {
            StarSetting = new StarSetting(),
            AgentSetting = new StarAgentSetting(),
            Log = Logger.Null,
        };
        svr.Register(svc, null);
        svr.Start();

        using var client = new ApiClient($"udp://127.0.0.1:{svr.Port}") { Timeout = 5_000 };
        var server = "http://127.0.0.1:6600";

        // 不带令牌
        var ex1 = await Assert.ThrowsAnyAsync<Exception>(() => client.InvokeAsync<String>("SetServer", new Dictionary<String, Object?> { ["server"] = server }));
        Assert.Contains("token", ex1.Message, StringComparison.OrdinalIgnoreCase);

        // 令牌错误
        var ex2 = await Assert.ThrowsAnyAsync<Exception>(() => client.InvokeAsync<String>("SetServer", new Dictionary<String, Object?> { ["server"] = server, ["token"] = "not-the-token" }));
        Assert.Contains("token", ex2.Message, StringComparison.OrdinalIgnoreCase);

        // 令牌正确
        var rs = await client.InvokeAsync<String>("SetServer", new Dictionary<String, Object?> { ["server"] = server, ["token"] = token });
        Assert.Equal(server, rs);
        Assert.Equal(server, svc.StarSetting.Server);
    }
}
