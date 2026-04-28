using System;
using System.Threading.Tasks;
using NewLife;
using NewLife.Security;
using Stardust;
using Stardust.Models;
using Xunit;

namespace ClientTest;

public class StarClientTests
{
    public String Server { get; set; } = "http://localhost:6600";
    //public StarClient Client { get; }

    public StarClientTests()
    {
        //Client = new StarClient(Server);
        //Client.Add("default", new Uri(Server));
    }

    [Fact]
    public void GetLoginInfoTest()
    {
        var client = new StarClient
        {
            Code = Rand.NextString(8),
            Secret = Rand.NextString(16)
        };

        var inf = client.BuildLoginRequest() as LoginInfo;
        Assert.NotNull(inf);
        Assert.NotNull(inf.Node);

        Assert.Equal(client.Code, inf.Code);
        Assert.Equal(client.Secret, inf.Secret);

        var node = client.GetNodeInfo();
        var mi = MachineInfo.Current;
        Assert.Equal(mi.UUID, node.UUID);
        Assert.Equal(mi.Guid, node.MachineGuid);
    }

    [Theory(DisplayName = "登录测试")]
    [InlineData("abcd", "1234")]
    [InlineData(null, "1234")]
    [InlineData("abcd", null)]
    public async Task LoginTest(String code, String secret)
    {
        var client = new StarClient(Server)
        {
            Code = code,
            Secret = secret
        };

        var rs = await client.Login();
        Assert.NotNull(rs);
        //Assert.NotNull(client.Info);
        Assert.True(client.Logined);
    }

    [Fact]
    public async Task LogoutTest()
    {
        var client = new StarClient(Server);

        await client.Login();
        await client.Logout("test");
    }

    [Fact]
    public void GetHeartInfoTest()
    {
        var client = new StarClient();
        var inf = client.BuildPingRequest() as PingInfo;
        Assert.NotNull(inf);
        Assert.NotEmpty(inf.Macs);
    }
}