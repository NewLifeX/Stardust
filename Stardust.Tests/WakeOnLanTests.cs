using System.ComponentModel;
using StarAgent;
using Xunit;

namespace Stardust.Tests;

/// <summary>局域网唤醒魔术包构造与解析测试</summary>
public class WakeOnLanTests
{
    [Fact]
    [DisplayName("魔术包为6个0xFF加16次MAC")]
    public void BuildPacket_Layout()
    {
        var mac = new Byte[] { 0xAA, 0xBB, 0xCC, 0xDD, 0xEE, 0xFF };

        var packet = WakeOnLan.BuildPacket(mac);

        Assert.Equal(102, packet.Length);
        for (var i = 0; i < 6; i++) Assert.Equal(0xFF, packet[i]);
        for (var i = 0; i < 16; i++)
        {
            for (var j = 0; j < 6; j++) Assert.Equal(mac[j], packet[6 + i * 6 + j]);
        }
    }

    [Fact]
    [DisplayName("MAC解析支持多种分隔符并去重")]
    public void ParseMacs_Formats()
    {
        var list = WakeOnLan.ParseMacs("AA-BB-CC-DD-EE-FF, aa:bb:cc:dd:ee:ff; aabb.ccdd.eeff 11-22-33-44-55-66");

        Assert.Equal(2, list.Count);
        Assert.Equal([0xAA, 0xBB, 0xCC, 0xDD, 0xEE, 0xFF], list[0]);
        Assert.Equal([0x11, 0x22, 0x33, 0x44, 0x55, 0x66], list[1]);
    }

    [Theory]
    [DisplayName("非法MAC一律忽略")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("ZZ-BB-CC-DD-EE-FF")]
    [InlineData("AA-BB-CC-DD-EE")]
    [InlineData("AA-BB-CC-DD-EE-FF-00")]
    [InlineData("hello world")]
    public void ParseMacs_Invalid(String? text) => Assert.Empty(WakeOnLan.ParseMacs(text));

    [Fact]
    [DisplayName("缺少MAC时构造魔术包抛异常")]
    public void BuildPacket_InvalidMac()
    {
        Assert.Throws<ArgumentNullException>(() => WakeOnLan.BuildPacket(null!));
        Assert.Throws<ArgumentException>(() => WakeOnLan.BuildPacket([0xAA]));
    }
}
