using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using NewLife;
using NewLife.Log;
using NewLife.Net;
using NewLife.Remoting;
using NewLife.Serialization;
using Stardust;
using Stardust.Models;
using Xunit;

namespace Stardust.Tests;

/// <summary>本地星尘客户端测试。覆盖本地探测的端口预检短路行为与跨命名策略的字段解析</summary>
public class LocalStarClientTests
{
    [Fact]
    public void GetInfo_NoListener_FastReturnNull()
    {
        // 本机没有监听星尘代理端口时，应预检短路直接返回null，不做1秒等待和3秒UDP超时
        var port = FindFreeUdpPort();
        if (port == 0) return;

        var old = LocalStarClient.Port;
        try
        {
            LocalStarClient.Port = port;

            var client = new LocalStarClient();
            var sw = Stopwatch.StartNew();
            var inf = client.GetInfo();
            sw.Stop();

            Assert.Null(inf);
            Assert.True(sw.ElapsedMilliseconds < 800, $"耗时 {sw.ElapsedMilliseconds}ms，预期端口预检短路快速返回");
        }
        finally
        {
            LocalStarClient.Port = old;
        }
    }

    [Fact]
    public async Task GetInfoAsync_NoListener_FastReturnNull()
    {
        // 异步路径同样需要端口预检短路
        var port = FindFreeUdpPort();
        if (port == 0) return;

        var old = LocalStarClient.Port;
        try
        {
            LocalStarClient.Port = port;

            var client = new LocalStarClient();
            var sw = Stopwatch.StartNew();
            var inf = await client.GetInfoAsync();
            sw.Stop();

            Assert.Null(inf);
            Assert.True(sw.ElapsedMilliseconds < 800, $"耗时 {sw.ElapsedMilliseconds}ms，预期端口预检短路快速返回");
        }
        finally
        {
            LocalStarClient.Port = old;
        }
    }

    [Theory]
    [DisplayName("GetInfo_服务端返回不同命名策略_均能读取关键字段")]
    [InlineData(PropertyNaming.None)]
    [InlineData(PropertyNaming.CamelCase)]
    public async Task GetInfo_IsCaseInsensitive(PropertyNaming naming)
    {
        var oldPort = LocalStarClient.Port;
        var oldNaming = JsonHelper.Default.Options.PropertyNaming;
        try
        {
            var json = new FastJson();
            json.Options.PropertyNaming = naming;

            using var svr = new ApiServer(new NetUri(NetType.Udp, IPAddress.Loopback.ToString(), 0))
            {
                Encoder = new JsonEncoder { JsonHost = json },
                Log = Logger.Null,
                EncoderLog = Logger.Null,
            };
            svr.Register(new FakeAgentService(), null);
            svr.Start();
            Assert.True(svr.Port > 0);

            // 客户端使用另一种命名策略，模拟宿主应用的全局设置，确保读写两端都不依赖同一策略
            JsonHelper.Default.Options.PropertyNaming = naming == PropertyNaming.None ? PropertyNaming.CamelCase : PropertyNaming.None;

            LocalStarClient.Port = svr.Port;
            var client = new LocalStarClient { Log = Logger.Null };
            var info = await client.GetInfoAsync();

            Assert.NotNull(info);
            Assert.Equal(10, info.ProcessId);
            Assert.Equal("3.7.2026.1031", info.Version);
            Assert.Equal("10.0.0.10", info.IP);
            Assert.Equal("96340C00", info.Code);
            Assert.Equal("http://s.newlifex.com:6600", info.Server);
        }
        finally
        {
            LocalStarClient.Port = oldPort;
            JsonHelper.Default.Options.PropertyNaming = oldNaming;
        }
    }

    /// <summary>模拟 StarAgent 本地 Info 接口</summary>
    [Api(null)]
    public class FakeAgentService
    {
        /// <summary>返回节点信息</summary>
        /// <param name="info">客户端信息</param>
        /// <returns></returns>
        [Api(nameof(Info))]
        public AgentInfo Info(AgentInfo info) => new()
        {
            ProcessId = 10,
            ProcessName = "dotnet",
            Version = "3.7.2026.1031",
            IP = "10.0.0.10",
            Server = "http://s.newlifex.com:6600",
            Code = "96340C00",
        };
    }

    /// <summary>查找一个当前未被监听的UDP端口</summary>
    private static Int32 FindFreeUdpPort()
    {
        var used = new HashSet<Int32>();
        try
        {
            var eps = IPGlobalProperties.GetIPGlobalProperties().GetActiveUdpListeners();
            foreach (var ep in eps) used.Add(ep.Port);
        }
        catch { }

        for (var port = 12000; port < 13000; port++)
        {
            if (!used.Contains(port)) return port;
        }
        return 0;
    }
}
