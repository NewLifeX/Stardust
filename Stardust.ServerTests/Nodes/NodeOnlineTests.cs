using System;
using System.Linq;
using Stardust.Data.Nodes;
using XCode;
using Xunit;

namespace ServerTest.Nodes;

/// <summary>节点在线实体单元测试</summary>
public class NodeOnlineTests
{
    [Fact(DisplayName = "超长DNS与网关在Valid时被截断为字段长度，心跳保存不再抛长度超限异常")]
    public void Valid_ExtraLongDnsAndGateway_Trimmed()
    {
        // 多网卡机器上报的DNS地址列表很容易超过字段长度，心跳保存时曾抛 ArgumentOutOfRangeException
        var dns = String.Join(",", Enumerable.Range(0, 20).Select(e => $"223.5.5.{e}"));
        var gateway = String.Join(",", Enumerable.Range(0, 10).Select(e => $"192.168.1.{e}/00-11-22-33-44-{e:D2}"));
        var dnsLen = NodeOnline._.Dns.Length;
        var gatewayLen = NodeOnline._.Gateway.Length;
        Assert.True(dns.Length > dnsLen);
        Assert.True(gateway.Length > gatewayLen);

        var entity = new NodeOnline
        {
            NodeID = 1,
            Name = "TestLongDns",
            Dns = dns,
            Gateway = gateway,
            // 避免Valid内部回查Node实体，单元测试不依赖数据库
            MemoryUsed = 1,
            SpaceUsed = 1,
        };

        // 生产回归：Valid 内部先把超长字段截断，再校验字段长度，不应抛出 ArgumentOutOfRangeException
        entity.Valid(DataMethod.Update);

        Assert.Equal(dnsLen, entity.Dns.Length);
        Assert.Equal(dns[..dnsLen], entity.Dns);
        Assert.Equal(gatewayLen, entity.Gateway.Length);
        Assert.Equal(gateway[..gatewayLen], entity.Gateway);
    }
}
