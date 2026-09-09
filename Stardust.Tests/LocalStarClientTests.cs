using System.Diagnostics;
using System.Net.NetworkInformation;
using Stardust;
using Xunit;

namespace Stardust.Tests;

/// <summary>本地星尘客户端测试。覆盖本地探测的端口预检短路行为</summary>
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
