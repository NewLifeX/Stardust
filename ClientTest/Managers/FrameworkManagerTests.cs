using System;
using System.Linq;
using NewLife;
using Stardust.Managers;
using Xunit;

namespace ClientTest.Managers;

public class FrameworkManagerTests
{
    [Fact(DisplayName = "获取本机已安装框架版本列表")]
    public void GetAllVersions()
    {
        var mgr = new FrameworkManager();

        var vers = mgr.GetAllVersions();

        Assert.NotNull(vers);
        Assert.NotEmpty(vers);
        Assert.All(vers, e => Assert.False(e.IsNullOrEmpty()));
    }

    [Fact(DisplayName = "获取框架版本列表可重复调用且结果稳定")]
    public void GetAllVersions_Repeatable()
    {
        var mgr = new FrameworkManager();

        var first = mgr.GetAllVersions();
        var second = mgr.GetAllVersions();

        Assert.Equal(first.OrderBy(e => e), second.OrderBy(e => e));
    }
}
