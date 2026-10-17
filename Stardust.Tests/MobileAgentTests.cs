using System.ComponentModel;
using StarAgent.Mobile;
using Stardust.Models;
using Xunit;

namespace Stardust.Tests;

/// <summary>移动代理核心测试</summary>
public class MobileAgentTests
{
    private class FakePlatform : IMobilePlatform
    {
        public String Name => "Test";
        public String DeviceId => "test-device";
        public String DeviceName => "TestPhone";
        public String OSName => "TestOS";
        public String OSVersion => "1.0";
        public String Architecture => "arm64";

        public String GetDataPath() => Path.Combine(Path.GetTempPath(), "staragent-tests");

        public void FillNodeInfo(NodeInfo info)
        {
            info.UUID = DeviceId;
            info.Memory = 1024;
        }

        public void FillPingInfo(PingInfo info)
        {
            info.Memory = 2048;
            info.CpuRate = 12.5;
            info.Battery = 80;
        }

        public void RestartApp() { }
    }

    [Fact]
    [DisplayName("登录请求填充移动设备信息")]
    public void BuildLoginRequest_UsesPlatformInfo()
    {
        var set = new MobileAgentSetting { Server = "http://127.0.0.1:6600", ProductCode = "StarAgent" };
        var client = new MobileStarClient(set, new FakePlatform());

        var request = Assert.IsType<LoginInfo>(client.BuildLoginRequest());

        Assert.Equal("StarAgent", request.ProductCode);
        Assert.NotNull(request.Node);
        Assert.Equal("test-device", request.Node!.UUID);
        Assert.Equal("TestOS", request.Node.OSName);
        Assert.Equal("TestPhone", request.Node.MachineName);
        Assert.Equal(1024UL, request.Node.Memory);
    }

    [Fact]
    [DisplayName("识别iOS操作系统类型")]
    public void OSKindHelper_iOS()
    {
        Assert.Equal(OSKinds.iOS, OSKindHelper.Parse("iOS", "17.4"));
        Assert.Equal(OSKinds.iOS, OSKindHelper.Parse("iPhone OS", "16.0"));
        Assert.Equal(OSKinds.iOS, OSKindHelper.Parse("iPadOS", "17.0"));
    }

    [Fact]
    [DisplayName("移动平台不使用RID部署")]
    public void GetRID_Mobile_ReturnsAny()
    {
        Assert.Equal([RuntimeIdentifier.Any], OSKindHelper.GetRID(OSKinds.Android, "arm64"));
        Assert.Equal([RuntimeIdentifier.Any], OSKindHelper.GetRID(OSKinds.iOS, "arm64"));
    }

    [Fact]
    [DisplayName("心跳请求填充移动指标")]
    public void BuildPingRequest_UsesPlatformMetrics()
    {
        var client = new MobileStarClient(new MobileAgentSetting(), new FakePlatform());

        var request = Assert.IsType<PingInfo>(client.BuildPingRequest());

        Assert.Equal(2048UL, request.Memory);
        Assert.Equal(12.5, request.CpuRate);
        Assert.Equal(80, request.Battery);
    }
}