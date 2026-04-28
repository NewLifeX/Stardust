using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Stardust.Data.Nodes;
using Stardust.Models;
using Xunit;

namespace Stardust.ServerTests;

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
}
