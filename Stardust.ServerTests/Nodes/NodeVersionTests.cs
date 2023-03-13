using NewLife.Reflection;
using Stardust.Data.Nodes;
using Stardust.Models;
using Xunit;

namespace Stardust.ServerTests.Nodes;

public class NodeVersionTests
{
    [Fact]
    public void NodeMatch()
    {
        var nv = new NodeVersion
        {
            Strategy = "node=*stone-*",
        };
        nv.Invoke("OnLoad");

        var rs = nv.Match(new Node { Code = "xxx-stone-yyy" });
        Assert.True(rs);

        rs = nv.Match(new Node { Code = "xxx-stoneyyy" });
        Assert.False(rs);
    }

    [Fact]
    public void VersionMatch()
    {
        var nv = new NodeVersion
        {
            Strategy = "version=2.*.2022.*",
        };
        nv.Invoke("OnLoad");

        var rs = nv.Match(new Node { Version = "2.5.2022.0104" });
        Assert.True(rs);

        rs = nv.Match(new Node { Version = "3.1.2022.0308" });
        Assert.False(rs);
    }

    [Fact]
    public void VersionMatch2()
    {
        var nv = new NodeVersion
        {
            Strategy = "version<=3.0",
        };
        nv.Invoke("OnLoad");

        var rs = nv.Match(new Node { Version = "2.5.2022.0104" });
        Assert.True(rs);

        rs = nv.Match(new Node { Version = "3.1.2022.0308" });
        Assert.False(rs);
    }

    [Fact]
    public void VersionMatch3()
    {
        var nv = new NodeVersion
        {
            Strategy = "version>=2.8",
        };
        nv.Invoke("OnLoad");

        var rs = nv.Match(new Node { Version = "3.1.2022.0308" });
        Assert.True(rs);

        rs = nv.Match(new Node { Version = "2.5.2022.0104" });
        Assert.False(rs);
    }

    [Fact]
    public void OsKindMatch()
    {
        var nv = new NodeVersion
        {
            Strategy = "oskind=winxp,win7",
        };
        nv.Invoke("OnLoad");

        var rs = nv.Match(new Node { OSKind = OSKinds.Win7 });
        Assert.True(rs);

        rs = nv.Match(new Node { OSKind = OSKinds.Win71 });
        Assert.False(rs);
    }
}