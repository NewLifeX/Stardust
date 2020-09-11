using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using NewLife.Log;
using NewLife.Serialization;
using Stardust.Models;
using Xunit;

namespace ClientTest
{
    public class AppInfoTests
    {
        [Fact]
        public void Test()
        {
            //var p = Process.GetCurrentProcess();

            foreach (var item in Process.GetProcesses())
            {
                //Console.WriteLine(item);
                var pi = new AppInfo(item);
                if (pi.ProcessorTime > 0) XTrace.WriteLine(pi.ToJson());
            }
        }
    }
}
