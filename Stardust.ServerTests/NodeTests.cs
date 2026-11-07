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

    [Fact(DisplayName = "策略版本标签与文件版本不一致时，节点上报新版本视为升级生效并停止重试")]
    public void Fill_StrategyLabelVersion_StopsRetry()
    {
        // 复现：升级策略版本号为 agent60-1106 这类标签，节点重启后上报程序集文件版本 3.7.2026.1106，
        // 两者永远不相等，导致服务端一直判定升级未生效，把已经升级成功的节点反复重推直到次数用尽
        var node = new Node
        {
            Code = "test-fill-" + Guid.NewGuid().ToString("n")[..8],
            Name = "test-fill",
            Version = "3.7.2026.0728",
            LastVersion = "agent60-1106",
            UpgradeAttempts = 3,
            LastUpgradeKey = "agent60-1106:7:ABCDEF0123456789",
            Enable = true,
        };
        node.Insert();

        try
        {
            node.Fill(new NodeInfo { Version = "3.7.2026.1106" });

            Assert.Equal("3.7.2026.1106", node.Version);
            Assert.Equal(Node.MaxUpgradeAttempts, node.UpgradeAttempts);
            Assert.Equal("agent60-1106:7:ABCDEF0123456789", node.LastUpgradeKey);
        }
        finally
        {
            NodeHistory.Delete(NodeHistory._.NodeID == node.ID);
            node.Delete();
        }
    }

    [Fact(DisplayName = "节点上报版本回退时清空升级去重状态，允许重新评估")]
    public void Fill_VersionRollback_ResetsRetryState()
    {
        var node = new Node
        {
            Code = "test-fill-" + Guid.NewGuid().ToString("n")[..8],
            Name = "test-fill",
            Version = "3.7.2026.1106",
            LastVersion = "agent60-1106",
            UpgradeAttempts = Node.MaxUpgradeAttempts,
            LastUpgradeKey = "agent60-1106:7:ABCDEF0123456789",
            Enable = true,
        };
        node.Insert();

        try
        {
            node.Fill(new NodeInfo { Version = "3.7.2026.0728" });

            Assert.Equal("3.7.2026.0728", node.Version);
            Assert.Equal(0, node.UpgradeAttempts);
            Assert.Null(node.LastUpgradeKey);
        }
        finally
        {
            NodeHistory.Delete(NodeHistory._.NodeID == node.ID);
            node.Delete();
        }
    }

    [Fact(DisplayName = "节点版本未变化时不改动升级状态")]
    public void Fill_SameVersion_KeepsRetryState()
    {
        var node = new Node
        {
            Code = "test-fill-" + Guid.NewGuid().ToString("n")[..8],
            Name = "test-fill",
            Version = "3.7.2026.0728",
            LastVersion = "agent60-1106",
            UpgradeAttempts = 2,
            LastUpgradeKey = "agent60-1106:7:ABCDEF0123456789",
        };

        node.Fill(new NodeInfo { Version = "3.7.2026.0728" });

        Assert.Equal(2, node.UpgradeAttempts);
        Assert.Equal("agent60-1106:7:ABCDEF0123456789", node.LastUpgradeKey);
    }
}

