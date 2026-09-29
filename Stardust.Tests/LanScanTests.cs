using System.ComponentModel;
using System.Net;
using AgentExpansion;
using NewLife.Log;
using Xunit;

namespace Stardust.Tests;

/// <summary>局域网扫描地址解析测试</summary>
/// <remarks>
/// AgentExpansion 插件只扫描 RFC1918 局域网网段，链路本地 169.254.0.0/16 等地址必须排除，
/// 否则无线直连/蓝牙/虚拟网卡的 /16 掩码会展开成六万多个地址，导致每小时一轮的全网段探测刷爆日志。
/// 通过链接插件源码直测（AgentExpansion 为插件工程，不便于直接引用）。
/// </remarks>
public class LanScanTests
{
    [Theory]
    [InlineData("10.0.0.1", true)]
    [InlineData("10.255.255.254", true)]
    [InlineData("172.15.255.255", false)]
    [InlineData("172.16.0.1", true)]
    [InlineData("172.31.255.254", true)]
    [InlineData("172.32.0.1", false)]
    [InlineData("192.168.1.1", true)]
    [InlineData("192.169.1.1", false)]
    [DisplayName("IsLanAddress_RFC1918边界")]
    public void IsLanAddress_Rfc1918Boundary(String address, Boolean expected)
    {
        Assert.Equal(expected, LanScan.IsLanAddress(IPAddress.Parse(address)));
    }

    [Theory]
    [InlineData("169.254.0.1")]
    [InlineData("169.254.255.254")]
    [InlineData("127.0.0.1")]
    [InlineData("0.0.0.1")]
    [InlineData("100.64.0.1")]
    [InlineData("198.18.0.1")]
    [InlineData("224.0.0.1")]
    [InlineData("240.0.0.1")]
    [InlineData("255.255.255.255")]
    [InlineData("8.8.8.8")]
    [DisplayName("IsLanAddress_非局域网地址被排除")]
    public void IsLanAddress_NonLan_Excluded(String address)
    {
        Assert.False(LanScan.IsLanAddress(IPAddress.Parse(address)));
    }

    [Fact]
    [DisplayName("IsLanAddress_空值与IPv6被排除")]
    public void IsLanAddress_Null_Excluded()
    {
        Assert.False(LanScan.IsLanAddress(null));
        Assert.False(LanScan.IsLanAddress(IPAddress.IPv6Loopback));
    }

    [Fact]
    [DisplayName("ParseNetworks_空值_返回空集合")]
    public void ParseNetworks_Empty_ReturnsEmpty()
    {
        Assert.Empty(LanScan.ParseNetworks(null, Logger.Null));
        Assert.Empty(LanScan.ParseNetworks("", Logger.Null));
        Assert.Empty(LanScan.ParseNetworks("   ", Logger.Null));
    }

    [Fact]
    [DisplayName("ParseNetworks_非局域网网段被忽略")]
    public void ParseNetworks_NonLan_Ignored()
    {
        var ranges = LanScan.ParseNetworks("169.254.0.0/16,0.0.0.0/0,8.8.8.8,169.254.*.*", Logger.Null);

        Assert.Empty(ranges);
    }

    [Fact]
    [DisplayName("ParseNetworks_混合配置_只保留局域网")]
    public void ParseNetworks_Mixed_OnlyLanKept()
    {
        var ranges = LanScan.ParseNetworks("169.254.0.0/16,10.0.0.0/8,192.168.1.*", Logger.Null);

        Assert.Equal(2, ranges.Count);
        Assert.Equal(IPAddress.Parse("10.0.0.1"), ranges[0].Start);
        Assert.Equal(IPAddress.Parse("10.255.255.254"), ranges[0].End);
        Assert.Equal(IPAddress.Parse("192.168.1.1"), ranges[1].Start);
        Assert.Equal(IPAddress.Parse("192.168.1.254"), ranges[1].End);
    }

    [Fact]
    [DisplayName("ParseNetworks_尾部通配符_排除网络与广播地址")]
    public void ParseNetworks_TrailingWildcard_ExcludesNetworkAndBroadcast()
    {
        var ranges = LanScan.ParseNetworks("192.168.*.*", Logger.Null);

        var range = Assert.Single(ranges);
        Assert.Equal(IPAddress.Parse("192.168.0.1"), range.Start);
        Assert.Equal(IPAddress.Parse("192.168.255.254"), range.End);
    }

    [Fact]
    [DisplayName("ParseNetworks_非连续通配符_保持原区间")]
    public void ParseNetworks_NonContiguousWildcard_KeepsRawRange()
    {
        var ranges = LanScan.ParseNetworks("10.*.1.*", Logger.Null);

        var range = Assert.Single(ranges);
        Assert.Equal(IPAddress.Parse("10.0.1.0"), range.Start);
        Assert.Equal(IPAddress.Parse("10.255.1.255"), range.End);
    }

    [Fact]
    [DisplayName("ParseNetworks_单个地址_构成单点区间")]
    public void ParseNetworks_SingleAddress()
    {
        var ranges = LanScan.ParseNetworks("192.168.1.10", Logger.Null);

        var range = Assert.Single(ranges);
        Assert.Equal(IPAddress.Parse("192.168.1.10"), range.Start);
        Assert.Equal(IPAddress.Parse("192.168.1.10"), range.End);
    }

    [Fact]
    [DisplayName("FromCidr_排除网络地址与广播地址")]
    public void FromCidr_ExcludesNetworkAndBroadcast()
    {
        var range = NetworkRange.FromCidr(IPAddress.Parse("10.10.140.224"), 20);

        Assert.Equal(IPAddress.Parse("10.10.128.1"), range.Start);
        Assert.Equal(IPAddress.Parse("10.10.143.254"), range.End);
        Assert.Equal(4094, range.GetAddresses().Count());
    }

    [Fact]
    [DisplayName("Distinct_同网段重复_去重")]
    public void Distinct_SameRange_Deduplicated()
    {
        var ranges = LanScan.ParseNetworks("192.168.1.0/24,192.168.1.1/24,192.168.2.0/24", Logger.Null);
        Assert.Equal(3, ranges.Count);

        var distinct = LanScan.Distinct(ranges);
        Assert.Equal(2, distinct.Count);
    }

    [Fact]
    [DisplayName("GetTargets_超过上限_按上限截断")]
    public void GetTargets_OverLimit_Truncated()
    {
        var ranges = LanScan.ParseNetworks("192.168.1.0/24", Logger.Null);

        var all = LanScan.GetTargets(ranges, 0, Logger.Null).ToList();
        Assert.Equal(254, all.Count);

        var capped = LanScan.GetTargets(ranges, 5, Logger.Null).ToList();
        Assert.Equal(5, capped.Count);
        Assert.Equal(IPAddress.Parse("192.168.1.1"), capped[0]);
        Assert.Equal(IPAddress.Parse("192.168.1.5"), capped[4]);
    }

    [Fact]
    [DisplayName("GetLocalRanges_不含链路本地等非局域网网段")]
    public void GetLocalRanges_OnlyLan()
    {
        var ranges = LanScan.GetLocalRanges();

        // 本机网卡解析出的网段必须全部是局域网，绝不能包含 169.254.0.0/16
        foreach (var range in ranges)
        {
            Assert.True(LanScan.IsLanAddress(range.Start), $"网段 {range.Start} 不是局域网地址");
            Assert.True(LanScan.IsLanAddress(range.End), $"网段 {range.End} 不是局域网地址");
        }

        var huge = ranges.Where(e => e.Start.GetAddressBytes()[0] == 169).ToList();
        Assert.Empty(huge);
    }
}
