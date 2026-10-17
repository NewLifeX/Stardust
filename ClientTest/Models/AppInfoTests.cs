using System.Diagnostics;
using NewLife;
using NewLife.Log;
using NewLife.Serialization;
using Stardust;
using Stardust.Models;
using Xunit;

namespace ClientTest.Models;

public class AppInfoTests
{
    [Fact]
    public void Test()
    {
        // AppInfo 构造会枚举该进程的全部 TCP 连接，遍历所有进程是 O(n²) 系统调用，单元测试只取少量样本
        foreach (var p in Process.GetProcesses().Take(5))
        {
            var pi = new AppInfo(p);
            Assert.Equal(p.Id, pi.Id);
        }

        var current = new AppInfo(Process.GetCurrentProcess());
        Assert.Equal(Environment.ProcessId, current.Id);
        Assert.NotNull(current.CommandLine);
        Assert.NotEmpty(current.MachineName);
    }

    [Fact]
    public void GetProcessName()
    {
        var p = Process.GetCurrentProcess();

        var name = p.GetProcessName();
        Assert.Contains(name, new[] { "testhost", "exec" });
    }

    //[Fact]
    //public void GetStarAgentName()
    //{
    //    var flag = false;
    //    foreach (var p in Process.GetProcesses())
    //    {
    //        if (p.ProcessName == "dotnet")
    //        {
    //            var name = AppInfo.GetProcessName(p);
    //            if (name == "StarAgent")
    //            {
    //                flag = true;
    //                break;
    //            }
    //        }
    //    }

    //    Assert.True(flag);
    //}
}
