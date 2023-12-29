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

        nv = new NodeVersion
        {
            Strategy = "node=6234BE2A;version<=2.9.2023.0827",
        };
        nv.Invoke("OnLoad");

        rs = nv.Match(new Node { Code = "6234BE2A", Name = "aml", Version = "2.9.2023.0825" });
        Assert.True(rs);

        nv = new NodeVersion
        {
            Strategy = "node=*a2*;framework=6.*,7.*;oskind=ubuntu;version>=2.9.2023.1115",
        };
        nv.Invoke("OnLoad");

        rs = nv.Match(new Node { Code = "6234BE2A", OSKind = OSKinds.Ubuntu, Framework = "6.0", Name = "a2", Version = "2.9.2023.1115" });
        Assert.True(rs);
        rs = nv.Match(new Node { Code = "6234BE2A", OSKind = OSKinds.Ubuntu, Framework = "6.0", Name = "浇花a2", Version = "2.9.2023.1116" });
        Assert.True(rs);
        rs = nv.Match(new Node { Code = "6234BE2A", OSKind = OSKinds.Ubuntu, Framework = "7.0", Name = "a2-4g", Version = "2.9.2023.1115" });
        Assert.True(rs);
        rs = nv.Match(new Node { Code = "6234BE2A", OSKind = OSKinds.Ubuntu, Frameworks = "6.0.21,7.0.10", Name = "a2-4g", Version = "2.9.2023.1114" });
        Assert.False(rs);

        nv = new NodeVersion
        {
            Strategy = "node=*a2*;framework<=7.0;oskind=ubuntu;version>=2.9.2023.1115",
        };
        nv.Invoke("OnLoad");

        rs = nv.Match(new Node { Code = "6234BE2A", OSKind = OSKinds.Ubuntu, Framework = "7.0", Name = "a2", Version = "2.9.2023.1115" });
        Assert.True(rs);
        rs = nv.Match(new Node { Code = "6234BE2A", OSKind = OSKinds.Ubuntu, Framework = "8.0", Name = "a2", Version = "2.9.2023.1115" });
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