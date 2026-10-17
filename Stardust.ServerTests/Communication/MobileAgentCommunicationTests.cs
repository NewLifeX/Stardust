using System;
using System.Threading.Tasks;
using NewLife;
using NewLife.Log;
using NewLife.Remoting.Models;
using StarAgent.Mobile;
using Stardust.Data.Nodes;
using Stardust.Models;
using Xunit;

namespace ServerTest.Communication;

/// <summary>移动端星尘代理到服务端的端到端测试</summary>
/// <remarks>
/// 使用真实的 StarAgent.Mobile 客户端连接真实 StarServer（随机 TCP 端口），
/// 验证登录、自动注册、ProductCode、OSKind 与心跳报文。
/// </remarks>
[Collection("StarServer")]
public class MobileAgentCommunicationTests(StarServerFixture fixture)
{
    private sealed class FakePlatform(String osName) : IMobilePlatform
    {
        public String Name => osName;
        public String DeviceId { get; } = Guid.NewGuid().ToString("N");
        public String DeviceName => "Test Phone";
        public String OSName => osName;
        public String OSVersion => "14.0";
        public String Architecture => "Arm64";

        public String GetDataPath() => System.IO.Path.GetTempPath();

        public void FillNodeInfo(NodeInfo info)
        {
            info.Memory = 8UL * 1024 * 1024 * 1024;
            info.AvailableMemory = 4UL * 1024 * 1024 * 1024;
            info.FreeMemory = 2UL * 1024 * 1024 * 1024;
            info.TotalSize = 64UL * 1024 * 1024 * 1024;
            info.AvailableFreeSpace = 32UL * 1024 * 1024 * 1024;
        }

        public void FillPingInfo(PingInfo info)
        {
            info.Memory = 8UL * 1024 * 1024 * 1024;
            info.AvailableMemory = 4UL * 1024 * 1024 * 1024;
            info.FreeMemory = 2UL * 1024 * 1024 * 1024;
            info.TotalSize = 64UL * 1024 * 1024 * 1024;
            info.AvailableFreeSpace = 32UL * 1024 * 1024 * 1024;
            info.CpuRate = 12.5;
            info.Battery = 80;
        }

        public void RestartApp() { }
    }

    // ClientBase 首次登录只给 1 秒超时，CI 冷启动时服务端可能来不及响应，重试时超时递增
    private static async Task<ILoginResponse?> LoginWithRetry(MobileStarClient client)
    {
        for (var i = 0; i < 3; i++)
        {
            try
            {
                return await client.Login();
            }
            catch (XException) when (i < 2)
            {
                await Task.Delay(1_000);
            }
        }

        return null;
    }

    [Theory]
    [InlineData("Android", OSKinds.Android)]
    [InlineData("iOS", OSKinds.iOS)]
    public async Task MobileAgent_LoginAndPing(String osName, OSKinds expectedKind)
    {
        var platform = new FakePlatform(osName);
        var setting = new MobileAgentSetting
        {
            Server = fixture.ServerUrl,
            ProductCode = "StarAgent-Mobile",
        };

        using var client = new MobileStarClient(setting, platform) { Log = XTrace.Log };

        var rs = await LoginWithRetry(client);
        Assert.NotNull(rs);
        Assert.True(client.Logined, "移动代理登录失败");

        var node = Node.FindByCode(rs!.Code ?? client.Code);
        Assert.NotNull(node);

        try
        {
            Assert.Equal("StarAgent-Mobile", node!.ProductCode);
            Assert.Equal(expectedKind, node.OSKind);
            Assert.False(node.AlarmOnOffline, "移动节点应默认关闭下线告警");

            var ping = await client.Ping();
            Assert.NotNull(ping);
            Assert.True(ping!.ServerTime > 0);
        }
        finally
        {
            NodeOnline.FindByNodeId(node!.ID)?.Delete();
            node.Delete();
        }
    }
}