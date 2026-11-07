using System.ComponentModel;
using System.Net;
using NewLife.Log;
using NewLife.Net;
using NewLife.Remoting;
using StarAgent;
using Stardust.Models;
using Xunit;

namespace Stardust.Tests;

/// <summary>本地探测事件备注测试。无参探测（局域网广播搜索）没有请求体，需要记录来源地址</summary>
public class StarServiceProbeTests
{
    [Fact]
    [DisplayName("Info_无参探测_事件备注与日志补上来访地址")]
    public async Task Info_NoPayload_WritesSourceAddress()
    {
        var log = new CaptureLog();
        using var svr = LocalApiServer.CreateServer(new NetUri(NetType.Udp, IPAddress.Loopback.ToString(), 0), null, Logger.Null);
        using var svc = new StarService
        {
            StarSetting = new StarSetting(),
            AgentSetting = new StarAgentSetting(),
            Log = log,
        };
        svr.Register(svc, null);
        svr.Start();

        using var client = new ApiClient($"udp://127.0.0.1:{svr.Port}") { Timeout = 30_000 };

        // 带请求体的正常本地探测，不写补充日志
        var info = await client.InvokeAsync<AgentInfo>("Info", new AgentInfo { ProcessId = 123 });
        Assert.NotNull(info);
        Assert.Empty(log.Logs);

        // 无参探测（局域网广播搜索），补充来源地址
        info = await client.InvokeAsync<AgentInfo>("Info");
        Assert.NotNull(info);
        Assert.Contains(log.Logs, e => e.Contains("127.0.0.1"));
    }

    private sealed class CaptureLog : Logger
    {
        public List<String> Logs { get; } = [];

        protected override void OnWrite(LogLevel level, String format, params Object?[] args)
        {
            lock (Logs) Logs.Add(args == null || args.Length == 0 ? format : String.Format(format, args));
        }
    }
}
