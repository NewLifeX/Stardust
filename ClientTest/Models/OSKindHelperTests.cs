using Stardust.Models;
using Xunit;

namespace ClientTest.Models;

public class OSKindHelperTests
{
    #region Parse
    [Fact]
    [Trait("Category", "OSKindHelper")]
    public void ParseEmptyReturnsUnknown()
    {
        var result = OSKindHelper.Parse("", "");
        Assert.Equal(0, (Int32)result);
    }

    [Fact]
    [Trait("Category", "OSKindHelper")]
    public void ParseSmartOS()
    {
        Assert.Equal(OSKinds.SmartOS, OSKindHelper.Parse("SmartOS", ""));
        Assert.Equal(OSKinds.SmartOS, OSKindHelper.Parse("SmartA2", ""));
        Assert.Equal(OSKinds.SmartOS, OSKindHelper.Parse("SmartA4", ""));
    }

    [Fact]
    [Trait("Category", "OSKindHelper")]
    public void ParseWindows10()
    {
        Assert.Equal(OSKinds.Win10, OSKindHelper.Parse("Windows 10", "10.0"));
    }

    [Fact]
    [Trait("Category", "OSKindHelper")]
    public void ParseWindows11()
    {
        Assert.Equal(OSKinds.Win11, OSKindHelper.Parse("Windows 11", "10.0"));
    }

    [Fact]
    [Trait("Category", "OSKindHelper")]
    public void ParseAndroid()
    {
        Assert.Equal(OSKinds.Android, OSKindHelper.Parse("Android 11", ""));
    }

    [Fact]
    [Trait("Category", "OSKindHelper")]
    public void ParseMacOS()
    {
        Assert.Equal(OSKinds.MacOSX, OSKindHelper.Parse("macOS", "12.0"));
    }

    [Fact]
    [Trait("Category", "OSKindHelper")]
    public void ParseUbuntu()
    {
        Assert.Equal(OSKinds.Ubuntu, OSKindHelper.Parse("Ubuntu 22.04", "22.04"));
    }

    [Fact]
    [Trait("Category", "OSKindHelper")]
    public void ParseAlpine()
    {
        Assert.Equal(OSKinds.Alpine, OSKindHelper.Parse("Alpine Linux", "3.18"));
    }
    #endregion

    #region ParseWindows
    [Theory]
    [Trait("Category", "OSKindHelper")]
    [InlineData("Windows XP", "5.1", OSKinds.WinXP)]
    [InlineData("Windows Server 2003", "5.2", OSKinds.WinXP)]
    [InlineData("Windows 7", "6.1", OSKinds.Win7)]
    [InlineData("Windows 8", "6.2", OSKinds.Win7)]
    [InlineData("Windows Vista", "6.0", OSKinds.Win7)]
    [InlineData("Windows 10", "10.0", OSKinds.Win10)]
    [InlineData("Windows 11", "10.0", OSKinds.Win11)]
    [InlineData("Windows Server 2022", "10.0", OSKinds.Win2022)]
    [InlineData("Windows Server 2019", "10.0", OSKinds.Win2019)]
    [InlineData("Windows Server 2016", "10.0", OSKinds.Win2016)]
    [InlineData("Windows Server 2012", "6.3", OSKinds.Win2012)]
    [InlineData("Windows Server 2008", "6.1", OSKinds.Win2008)]
    public void ParseWindowsKnownVersions(String osName, String osVersion, OSKinds expected)
    {
        Assert.Equal(expected, OSKindHelper.ParseWindows(osName, osVersion));
    }

    [Fact]
    [Trait("Category", "OSKindHelper")]
    public void ParseWindowsReturnsZeroForNonWindows()
    {
        Assert.Equal(0, (Int32)OSKindHelper.ParseWindows("Ubuntu 22.04", "22.04"));
        Assert.Equal(0, (Int32)OSKindHelper.ParseWindows("Linux", "5.0"));
    }

    [Fact]
    [Trait("Category", "OSKindHelper")]
    public void ParseWindowsServerGenericReturnsWinServer()
    {
        Assert.Equal(OSKinds.WinServer, OSKindHelper.ParseWindows("Windows Server 2099", "10.0"));
    }

    [Fact]
    [Trait("Category", "OSKindHelper")]
    public void ParseWindowsByVersionNumber10()
    {
        Assert.Equal(OSKinds.Win10, OSKindHelper.ParseWindows("Win32NT", "10.0.19041"));
    }
    #endregion

    #region ParseLinux
    [Theory]
    [Trait("Category", "OSKindHelper")]
    [InlineData("Alpine Linux", "3.18", OSKinds.Alpine)]
    [InlineData("ArchLinux", "", OSKinds.ArchLinux)]
    [InlineData("Ubuntu 22.04", "22.04", OSKinds.Ubuntu)]
    [InlineData("Debian GNU/Linux", "11", OSKinds.Debian)]
    [InlineData("Red Hat Enterprise Linux", "8.0", OSKinds.RedHat)]
    [InlineData("CentOS Linux 7", "7", OSKinds.CentOS)]
    [InlineData("Fedora 38", "38", OSKinds.Fedora)]
    [InlineData("Raspbian GNU/Linux 11", "11", OSKinds.Raspbian)]
    [InlineData("Deepin 20", "", OSKinds.Deepin)]
    [InlineData("UOS Desktop", "", OSKinds.UOS)]
    [InlineData("Kylin V10", "", OSKinds.Kylin)]
    [InlineData("openEuler 22.03", "", OSKinds.OpenEuler)]
    [InlineData("AlmaLinux 9", "9", OSKinds.Alma)]
    [InlineData("Rocky Linux 9", "9", OSKinds.Rocky)]
    public void ParseLinuxDistributions(String osName, String osVersion, OSKinds expected)
    {
        Assert.Equal(expected, OSKindHelper.ParseLinux(osName, osVersion));
    }

    [Fact]
    [Trait("Category", "OSKindHelper")]
    public void ParseLinuxGenericContainsLinux()
    {
        var result = OSKindHelper.ParseLinux("SomeUnknownLinux", "");
        Assert.Equal(OSKinds.Linux, result);
    }

    [Fact]
    [Trait("Category", "OSKindHelper")]
    public void ParseLinuxOpenWrt()
    {
        Assert.Equal(OSKinds.OpenWrt, OSKindHelper.ParseLinux("OpenWrt 22.03", "22.03"));
    }

    [Fact]
    [Trait("Category", "OSKindHelper")]
    public void ParseLinuxArmbian()
    {
        Assert.Equal(OSKinds.Armbian, OSKindHelper.ParseLinux("Armbian Jammy", "22.04"));
    }
    #endregion

    #region GetRID
    [Theory]
    [Trait("Category", "OSKindHelper")]
    [InlineData(OSKinds.Win10, "x64", RuntimeIdentifier.WinX64)]
    [InlineData(OSKinds.Win10, "x86", RuntimeIdentifier.WinX86)]
    [InlineData(OSKinds.Win10, "arm64", RuntimeIdentifier.WinArm64)]
    [InlineData(OSKinds.Win11, "x64", RuntimeIdentifier.WinX64)]
    public void GetRIDWindows(OSKinds kind, String arch, RuntimeIdentifier expectedFirst)
    {
        var rids = OSKindHelper.GetRID(kind, arch);
        Assert.NotEmpty(rids);
        Assert.Equal(expectedFirst, rids[0]);
    }

    [Theory]
    [Trait("Category", "OSKindHelper")]
    [InlineData(OSKinds.Ubuntu, "x64", RuntimeIdentifier.LinuxX64)]
    [InlineData(OSKinds.Ubuntu, "arm64", RuntimeIdentifier.LinuxArm64)]
    [InlineData(OSKinds.Ubuntu, "arm", RuntimeIdentifier.LinuxArm)]
    [InlineData(OSKinds.CentOS, "x64", RuntimeIdentifier.LinuxX64)]
    public void GetRIDLinux(OSKinds kind, String arch, RuntimeIdentifier expectedFirst)
    {
        var rids = OSKindHelper.GetRID(kind, arch);
        Assert.NotEmpty(rids);
        Assert.Equal(expectedFirst, rids[0]);
    }

    [Theory]
    [Trait("Category", "OSKindHelper")]
    [InlineData(OSKinds.Alpine, "x64", RuntimeIdentifier.LinuxMuslX64)]
    [InlineData(OSKinds.Alpine, "arm64", RuntimeIdentifier.LinuxMuslArm64)]
    [InlineData(OSKinds.Alpine, "arm", RuntimeIdentifier.LinuxMuslArm)]
    public void GetRIDAlpine(OSKinds kind, String arch, RuntimeIdentifier expectedFirst)
    {
        var rids = OSKindHelper.GetRID(kind, arch);
        Assert.NotEmpty(rids);
        Assert.Equal(expectedFirst, rids[0]);
    }

    [Theory]
    [Trait("Category", "OSKindHelper")]
    [InlineData(OSKinds.MacOSX, "x64", RuntimeIdentifier.OsxX64)]
    [InlineData(OSKinds.MacOSX, "arm64", RuntimeIdentifier.OsxArm64)]
    public void GetRIDOsx(OSKinds kind, String arch, RuntimeIdentifier expectedFirst)
    {
        var rids = OSKindHelper.GetRID(kind, arch);
        Assert.NotEmpty(rids);
        Assert.Equal(expectedFirst, rids[0]);
    }

    [Fact]
    [Trait("Category", "OSKindHelper")]
    public void GetRIDWithSpecificArchIncludesParentRID()
    {
        var rids = OSKindHelper.GetRID(OSKinds.Ubuntu, "x64");
        Assert.Equal(2, rids.Length);
        Assert.Equal(RuntimeIdentifier.LinuxX64, rids[0]);
        Assert.Equal(RuntimeIdentifier.Linux, rids[1]);
    }

    [Fact]
    [Trait("Category", "OSKindHelper")]
    public void GetRIDUnknownArchReturnsParentOnly()
    {
        var rids = OSKindHelper.GetRID(OSKinds.Ubuntu, "unknown");
        Assert.Single(rids);
        Assert.Equal(RuntimeIdentifier.Linux, rids[0]);
    }

    [Fact]
    [Trait("Category", "OSKindHelper")]
    public void GetRIDSmartOSIsLinux()
    {
        var rids = OSKindHelper.GetRID(OSKinds.SmartOS, "x64");
        Assert.Contains(RuntimeIdentifier.LinuxX64, rids);
    }
    #endregion
}
