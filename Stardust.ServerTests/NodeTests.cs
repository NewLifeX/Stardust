using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Stardust.Data.Nodes;
using Stardust.Models;
using XCode;
using Xunit;

namespace ServerTest;

public class NodeTests
{
    [Fact]
    public void TestDriveSize()
    {
        var inf = new NodeInfo
        {
            DriveInfo = "C:\\[NTFS]=8.50G/200.00G,D:\\[NTFS]=80.34G/275.45G",
        };
        var node = new Node();
        node.Fill(inf);

        Assert.NotEmpty(node.DriveInfo);
        Assert.Equal(486861, node.DriveSize);
        Assert.Equal(Math.Round((200 + 275.45) * 1024), node.DriveSize);
    }

    [Fact(DisplayName = "超长DNS在Valid时被截断为字段长度，节点登录不再抛长度超限异常")]
    public void Valid_ExtraLongDns_Trimmed()
    {
        // 多网卡机器上报的DNS地址列表很容易超过字段长度，登录保存时曾抛 ArgumentOutOfRangeException
        var dns = String.Join(",", Enumerable.Range(0, 20).Select(e => $"223.5.5.{e}"));
        var len = Node._.Dns.Length;
        Assert.True(len >= 200);
        Assert.True(dns.Length > len);

        var node = new Node { Name = "TestLongDns", Code = "TestLongDns", Dns = dns };

        // 生产回归：Valid 内部先把超长字段截断，再校验字段长度，不应抛出 ArgumentOutOfRangeException
        node.Valid(DataMethod.Update);

        Assert.Equal(len, node.Dns.Length);
        Assert.Equal(dns[..len], node.Dns);
    }
}
