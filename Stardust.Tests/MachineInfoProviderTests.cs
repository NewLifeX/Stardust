using NewLife;
using Stardust.Managers;
using Xunit;

namespace Stardust.Tests;

/// <summary>机器信息提供者测试。覆盖设备树 compatible 解析</summary>
public class MachineInfoProviderTests
{
    [Fact(DisplayName = "解析多段compatible")]
    public void ParseCompatibleMultiTest()
    {
        // GT68XX 实际格式：NUL 分隔两条，不得拼成 gt685xrockchip 之类的错误主板名
        var mi = new MachineInfo();

        MachineInfoProvider.ParseCompatible("htnice,gt685x\0rockchip,rk3568", mi);

        Assert.Equal("htnice", mi.Vendor);
        Assert.Equal("gt685x", mi.Board);
        Assert.Equal("rk3568", mi.Processor);
    }

    [Fact(DisplayName = "解析单条compatible")]
    public void ParseCompatibleSingleTest()
    {
        // 单条“厂商,芯片”不设置主板，主板留给 os-release 提供
        var mi = new MachineInfo();

        MachineInfoProvider.ParseCompatible("nuvoton,ma35d1", mi);

        Assert.Equal("nuvoton", mi.Vendor);
        Assert.Null(mi.Board);
        Assert.Equal("ma35d1", mi.Processor);
    }

    [Fact(DisplayName = "解析空compatible")]
    public void ParseCompatibleEmptyTest()
    {
        var mi = new MachineInfo();

        MachineInfoProvider.ParseCompatible("", mi);

        Assert.Null(mi.Vendor);
        Assert.Null(mi.Board);
        Assert.Null(mi.Processor);
    }

    [Fact(DisplayName = "磁盘标识优先硬件标识")]
    public void GetDiskIdTest()
    {
        // 仅 Linux 使用 by-id/by-uuid 逻辑
        if (!Runtime.Linux) return;

        var diskId = MachineInfoProvider.GetDiskId();
        Assert.NotNull(diskId);

        // 存在硬件标识时不应使用文件系统 UUID
        var byId = MachineInfo.GetFiles("/dev/disk/by-id", true);
        if (byId.Count > 0)
        {
            foreach (var item in diskId.Split(','))
            {
                Assert.False(item.Length == 36 && item[8] == '-', $"不应使用文件系统UUID：{item}");
            }
        }
    }
}
