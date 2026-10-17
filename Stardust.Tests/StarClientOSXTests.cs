using System.ComponentModel;
using Stardust.Models;
using Xunit;

namespace Stardust.Tests;

/// <summary>macOS平台采集解析测试</summary>
public class StarClientOSXTests
{
    [Fact]
    [DisplayName("解析top输出的CPU空闲率")]
    public void ParseTopIdle_Normal()
    {
        var output = "Processes: 400 total, 2 running, 398 sleeping, 2000 threads\n" +
                     "Load Avg: 1.23, 2.34, 3.45\n" +
                     "CPU usage: 3.12% user, 6.25% sys, 90.62% idle\n" +
                     "SharedLibs: 200M resident, 30M data, 0B linkedit.";

        Assert.Equal(90.62, StarClient.ParseTopIdle(output), 3);
    }

    [Fact]
    [DisplayName("解析top输出_无CPU行时返回负一")]
    public void ParseTopIdle_Missing()
    {
        Assert.Equal(-1, StarClient.ParseTopIdle("no cpu line"));
        Assert.Equal(-1, StarClient.ParseTopIdle(null));
    }

    [Fact]
    [DisplayName("解析vm_stat输出_计算可用内存与空闲内存")]
    public void ParseVmStat_Normal()
    {
        var output = "Mach Virtual Memory Statistics: (page size of 16384 bytes)\n" +
                     "Pages free:                               1000.\n" +
                     "Pages active:                             2000.\n" +
                     "Pages inactive:                           3000.\n" +
                     "Pages speculative:                         500.\n" +
                     "Pages wired down:                         1500.\n" +
                     "\"Translation faults\":                   123456.\n" +
                     "Pages occupied by compressor:             1200.";

        var (available, free) = StarClient.ParseVmStat(output);

        Assert.Equal((UInt64)(1000 * 16384), free);
        Assert.Equal((UInt64)((1000 + 3000 + 500) * 16384), available);
    }

    [Fact]
    [DisplayName("解析vm_stat输出_空内容返回零")]
    public void ParseVmStat_Empty()
    {
        var (available, free) = StarClient.ParseVmStat("");

        Assert.Equal(0UL, available);
        Assert.Equal(0UL, free);
    }

    [Fact]
    [DisplayName("解析iostat输出_取最后一次采样并汇总tps")]
    public void ParseIostatTps_Normal()
    {
        var output = "              disk0               disk1\n" +
                     "     KB/t  tps  MB/s     KB/t  tps  MB/s\n" +
                     "   13.09    2  0.03     0.00    0  0.00\n" +
                     "    0.00    3  0.00     0.00    4  0.00";

        Assert.Equal(7, StarClient.ParseIostatTps(output));
    }

    [Fact]
    [DisplayName("解析iostat输出_只有表头时返回零")]
    public void ParseIostatTps_HeaderOnly()
    {
        var output = "              disk0\n" +
                     "     KB/t  tps  MB/s";

        Assert.Equal(0, StarClient.ParseIostatTps(output));
    }

    [Fact]
    [DisplayName("解析pmset输出_笔记本电脑电量")]
    public void ParseBatteryPercent_Laptop()
    {
        var output = "Now drawing from 'Battery Power'\n" +
                     " -InternalBattery-0 (id=4653155)\t82%; discharging; 4:12 remaining present: true";

        Assert.Equal(82, StarClient.ParseBatteryPercent(output));
    }

    [Fact]
    [DisplayName("解析pmset输出_台式机无电池返回负一")]
    public void ParseBatteryPercent_Desktop()
    {
        var output = "Now drawing from 'AC Power'";

        Assert.Equal(-1, StarClient.ParseBatteryPercent(output));
    }

    [Fact]
    [DisplayName("解析vm.loadavg输出")]
    public void ParseLoad_Normal()
    {
        Assert.Equal(1.23, StarClient.ParseLoad("{ 1.23 2.34 3.45 }"), 2);
        Assert.Equal(0, StarClient.ParseLoad(null));
    }

    [Fact]
    [DisplayName("解析显卡信息_支持多显卡去重")]
    public void ParseGpuInfo_Multiple()
    {
        var output = "Graphics/Displays:\n" +
                     "\n" +
                     "    Apple M2 Pro:\n" +
                     "\n" +
                     "      Chipset Model: Apple M2 Pro\n" +
                     "      Type: GPU\n" +
                     "      Bus: Built-In\n" +
                     "\n" +
                     "    AMD Radeon Pro 5500M:\n" +
                     "\n" +
                     "      Chipset Model: AMD Radeon Pro 5500M\n" +
                     "      Type: GPU";

        Assert.Equal("Apple M2 Pro,AMD Radeon Pro 5500M", StarClient.ParseGpuInfo(output));
    }

    [Fact]
    [DisplayName("解析WiFi名称与信号强度")]
    public void ParseWifi_Normal()
    {
        Assert.Equal("MyWiFi", StarClient.ParseAirportNetwork("Current Wi-Fi Network: MyWiFi"));

        var output = "Current Network Information:\n" +
                     "  MyWiFi:\n" +
                     "    PHY Mode: 802.11ax\n" +
                     "    Signal / Noise: -55 dBm / -90 dBm";

        Assert.Equal(-55, StarClient.ParseWifiSignal(output));
    }

    [Fact]
    [DisplayName("识别macOS操作系统类型")]
    public void OSKindHelper_MacOS()
    {
        Assert.Equal(OSKinds.MacOSX, OSKindHelper.Parse("macOS", "14.5"));
        Assert.Equal(OSKinds.MacOSX, OSKindHelper.Parse("Mac OS X", "10.15.7"));
    }
}