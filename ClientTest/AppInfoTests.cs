using System.Diagnostics;
using NewLife;
using NewLife.Log;
using NewLife.Serialization;
using Stardust;
using Stardust.Models;
using Xunit;

namespace ClientTest;

public class AppInfoTests
{
    [Fact]
    public void Test()
    {
        foreach (var p in Process.GetProcesses())
        {
            //Console.WriteLine(p);
            var pi = new AppInfo(p);
            if (pi.ProcessorTime > 0) XTrace.WriteLine(pi.ToJson());
        }
    }

    [Fact]
    public void GetProcessName()
    {
        var p = Process.GetCurrentProcess();

        var name = p.GetProcessName();
        Assert.Equal("testhost", name);
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
