using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Xunit;

namespace Stardust.ServerTests.Nodes;

public class NodeStatTests
{
    [Fact]
    public void Test1()
    {
        var name = "AMD Ryzen 7 2700 Eight-Core Processor";

        var p = name.IndexOf("-Core");
        if (p > 0) p = name.LastIndexOf(' ', p);
        if (p > 0) name = name[..p].Trim();

        Assert.Equal("AMD Ryzen 7 2700", name);
    }
}
