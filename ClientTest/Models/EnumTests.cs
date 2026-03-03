using System.ComponentModel;
using System.Reflection;
using Stardust.Models;
using Xunit;

namespace ClientTest.Models;

public class ProcessPriorityTests
{
    [Fact]
    [Trait("Category", "Enums")]
    public void ValuesAreCorrect()
    {
        Assert.Equal(-2, (Int32)ProcessPriority.Idle);
        Assert.Equal(-1, (Int32)ProcessPriority.BelowNormal);
        Assert.Equal(0, (Int32)ProcessPriority.Normal);
        Assert.Equal(1, (Int32)ProcessPriority.AboveNormal);
        Assert.Equal(2, (Int32)ProcessPriority.High);
        Assert.Equal(3, (Int32)ProcessPriority.RealTime);
    }

    [Theory]
    [Trait("Category", "Enums")]
    [InlineData(ProcessPriority.Idle, "空闲")]
    [InlineData(ProcessPriority.BelowNormal, "较低")]
    [InlineData(ProcessPriority.Normal, "正常")]
    [InlineData(ProcessPriority.AboveNormal, "较高")]
    [InlineData(ProcessPriority.High, "高")]
    [InlineData(ProcessPriority.RealTime, "实时")]
    public void DescriptionAttributeIsCorrect(ProcessPriority priority, String expectedDescription)
    {
        var field = typeof(ProcessPriority).GetField(priority.ToString())!;
        var attr = field.GetCustomAttribute<DescriptionAttribute>();

        Assert.NotNull(attr);
        Assert.Equal(expectedDescription, attr.Description);
    }
}

public class DeployModesTests
{
    [Fact]
    [Trait("Category", "Enums")]
    public void ValuesAreCorrect()
    {
        Assert.Equal(1, (Int32)DeployModes.Partial);
        Assert.Equal(2, (Int32)DeployModes.Standard);
        Assert.Equal(3, (Int32)DeployModes.Full);
    }

    [Theory]
    [Trait("Category", "Enums")]
    [InlineData(DeployModes.Partial, "增量包")]
    [InlineData(DeployModes.Standard, "标准包")]
    [InlineData(DeployModes.Full, "完整包")]
    public void DescriptionAttributeIsCorrect(DeployModes mode, String expectedDescription)
    {
        var field = typeof(DeployModes).GetField(mode.ToString())!;
        var attr = field.GetCustomAttribute<DescriptionAttribute>();

        Assert.NotNull(attr);
        Assert.Equal(expectedDescription, attr.Description);
    }
}

public class DeployModeTests
{
    [Fact]
    [Trait("Category", "Enums")]
    public void ValuesAreCorrect()
    {
        Assert.Equal(0, (Int32)DeployMode.Default);
        Assert.Equal(10, (Int32)DeployMode.Standard);
        Assert.Equal(11, (Int32)DeployMode.Shadow);
        Assert.Equal(12, (Int32)DeployMode.Hosted);
        Assert.Equal(13, (Int32)DeployMode.Task);
    }

    [Fact]
    [Trait("Category", "Enums")]
    public void NewModesAreAboveTen()
    {
        Assert.True((Int32)DeployMode.Standard >= 10);
        Assert.True((Int32)DeployMode.Shadow >= 10);
        Assert.True((Int32)DeployMode.Hosted >= 10);
        Assert.True((Int32)DeployMode.Task >= 10);
    }
}

public class RuntimeIdentifierTests
{
    [Fact]
    [Trait("Category", "Enums")]
    public void WinValuesAreInTens()
    {
        Assert.Equal(10, (Int32)RuntimeIdentifier.Win);
        Assert.Equal(11, (Int32)RuntimeIdentifier.WinX86);
        Assert.Equal(12, (Int32)RuntimeIdentifier.WinX64);
        Assert.Equal(13, (Int32)RuntimeIdentifier.WinArm);
        Assert.Equal(14, (Int32)RuntimeIdentifier.WinArm64);
    }

    [Fact]
    [Trait("Category", "Enums")]
    public void LinuxValuesAreInTwenties()
    {
        Assert.Equal(20, (Int32)RuntimeIdentifier.Linux);
        Assert.Equal(22, (Int32)RuntimeIdentifier.LinuxX64);
        Assert.Equal(23, (Int32)RuntimeIdentifier.LinuxArm);
        Assert.Equal(24, (Int32)RuntimeIdentifier.LinuxArm64);
        Assert.Equal(26, (Int32)RuntimeIdentifier.LinuxMips64);
        Assert.Equal(27, (Int32)RuntimeIdentifier.LinuxLA64);
        Assert.Equal(28, (Int32)RuntimeIdentifier.LinuxRiscV64);
    }

    [Fact]
    [Trait("Category", "Enums")]
    public void LinuxMuslValuesAreInThirties()
    {
        Assert.Equal(30, (Int32)RuntimeIdentifier.LinuxMusl);
        Assert.Equal(32, (Int32)RuntimeIdentifier.LinuxMuslX64);
        Assert.Equal(33, (Int32)RuntimeIdentifier.LinuxMuslArm);
        Assert.Equal(34, (Int32)RuntimeIdentifier.LinuxMuslArm64);
    }

    [Fact]
    [Trait("Category", "Enums")]
    public void OsxValuesAreInForties()
    {
        Assert.Equal(40, (Int32)RuntimeIdentifier.Osx);
        Assert.Equal(42, (Int32)RuntimeIdentifier.OsxX64);
        Assert.Equal(44, (Int32)RuntimeIdentifier.OsxArm64);
    }

    [Fact]
    [Trait("Category", "Enums")]
    public void AnyValueIsZero()
    {
        Assert.Equal(0, (Int32)RuntimeIdentifier.Any);
    }
}

public class OSKindsTests
{
    [Fact]
    [Trait("Category", "Enums")]
    public void UnknownIsZero()
    {
        Assert.Equal(0, (Int32)OSKinds.Unknown);
    }

    [Fact]
    [Trait("Category", "Enums")]
    public void Win10AndWin11AreSmallValues()
    {
        Assert.Equal(10, (Int32)OSKinds.Win10);
        Assert.Equal(11, (Int32)OSKinds.Win11);
    }

    [Fact]
    [Trait("Category", "Enums")]
    public void LinuxBaseValueIs100()
    {
        Assert.Equal(100, (Int32)OSKinds.Linux);
    }

    [Fact]
    [Trait("Category", "Enums")]
    public void AlpineValueIs90()
    {
        Assert.Equal(90, (Int32)OSKinds.Alpine);
    }

    [Fact]
    [Trait("Category", "Enums")]
    public void SmartOSValueIs40()
    {
        Assert.Equal(40, (Int32)OSKinds.SmartOS);
    }
}
