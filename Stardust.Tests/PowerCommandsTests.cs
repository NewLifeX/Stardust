using System.ComponentModel;
using StarAgent;
using Xunit;

namespace Stardust.Tests;

/// <summary>电源命令参数构造测试</summary>
public class PowerCommandsTests
{
    [Fact]
    [DisplayName("Windows重启命令必须一次携带强制参数")]
    public void GetWindowsRebootArgs_AlwaysForce()
    {
        Assert.Equal("-r -f -t 0", PowerCommands.GetWindowsRebootArgs(0));
        Assert.Equal("-r -f -t 30", PowerCommands.GetWindowsRebootArgs(30));
        Assert.Equal("-r -f -t 0", PowerCommands.GetWindowsRebootArgs(-1));
    }
}
