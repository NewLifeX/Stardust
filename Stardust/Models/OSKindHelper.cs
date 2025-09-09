using System.Linq;
using NewLife;

namespace Stardust.Models;

/// <summary>操作系统类型识别</summary>
public static class OSKindHelper
{
    /// <summary>识别操作系统类型</summary>
    /// <param name="osName"></param>
    /// <param name="osVersion"></param>
    /// <returns></returns>
    public static OSKinds Parse(String osName, String osVersion)
    {
        if (osName.IsNullOrEmpty()) return 0;

        if (osName.StartsWithIgnoreCase("SmartOS", "SmartA2", "SmartA4")) return OSKinds.SmartOS;

        var kind = ParseWindows(osName, osVersion);
        if (kind > 0) return kind;

        kind = ParseLinux(osName, osVersion);
        if (kind > 0) return kind;

        if (osName.IndexOf("Android", StringComparison.OrdinalIgnoreCase) >= 0) return OSKinds.Android;
        if (osName.IndexOf("Mac", StringComparison.InvariantCultureIgnoreCase) >= 0) return OSKinds.MacOSX;

        return 0;
    }

    /// <summary>识别Windows操作系统</summary>
    /// <param name="osName"></param>
    /// <param name="osVersion"></param>
    /// <returns></returns>
    public static OSKinds ParseWindows(String osName, String osVersion)
    {
        if (!osName.StartsWithIgnoreCase("Windows", "Win32NT")) return 0;

        osVersion += "";

        // 优先识别新系统
        if (osName.StartsWithIgnoreCase("Windows 11")) return OSKinds.Win11;
        if (osName.StartsWithIgnoreCase("Windows 10")) return OSKinds.Win10;

        if (osName.StartsWithIgnoreCase("Windows Server 2022")) return OSKinds.Win2022;
        if (osName.StartsWithIgnoreCase("Windows Server 2019")) return OSKinds.Win2019;
        if (osName.StartsWithIgnoreCase("Windows Server 2016")) return OSKinds.Win2016;
        if (osName.StartsWithIgnoreCase("Windows Server 2012")) return OSKinds.Win2012;

        if (osName.StartsWithIgnoreCase("Windows 8.1")) return OSKinds.Win81;
        if (osName.StartsWithIgnoreCase("Windows 8")) return OSKinds.Win8;

        if (osName.StartsWithIgnoreCase("Windows 7")) return osVersion.Contains("7601") ? OSKinds.Win71 : OSKinds.Win7;

        if (osName.StartsWithIgnoreCase("Windows Vista")) return OSKinds.WinVista;

        if (osName.StartsWithIgnoreCase("Windows Server 2008")) return OSKinds.Win2008;
        if (osName.StartsWithIgnoreCase("Windows Server 2003")) return OSKinds.Win2003;

        if (osName.StartsWithIgnoreCase("Windows XP")) return osName.Contains("SP3") ? OSKinds.WinXP3 : OSKinds.WinXP;

        // 根据版本识别
        var str = osVersion.Length < 3 ? osVersion : osVersion.Substring(0, 3);
        if (osName.Contains("Server"))
        {
            if (str.StartsWith("5.")) return OSKinds.Win2003;
            if (str.StartsWith("6.")) return OSKinds.Win2008;

            return OSKinds.WinServer;
        }

        return str switch
        {
            "10." => OSKinds.Win10,
            "6.3" => OSKinds.Win81,
            "6.23" => OSKinds.Win8,
            "6.1" => OSKinds.Win7,
            "6.0" => OSKinds.WinVista,
            "5.2" => OSKinds.Win2003,
            "5.1" => OSKinds.WinXP,
            _ => 0,
        };
    }

    /// <summary>识别Linux操作系统</summary>
    /// <param name="osName"></param>
    /// <param name="osVersion"></param>
    /// <returns></returns>
    public static OSKinds ParseLinux(String osName, String osVersion)
    {
        //if (!osName.Contains("Linux")) return 0;

        // 优先识别新系统
        if (osName.StartsWithIgnoreCase("Alpine") || osName.EndsWithIgnoreCase("(Alpine)")) return OSKinds.Alpine;
        if (osName.StartsWithIgnoreCase("Arch")) return OSKinds.ArchLinux;

        if (osName.StartsWithIgnoreCase("Ubuntu")) return OSKinds.Ubuntu;
        if (osName.StartsWithIgnoreCase("Debian")) return OSKinds.Debian;
        if (osName.Contains("Armbian")) return OSKinds.Armbian;
        if (osName.StartsWithIgnoreCase("Raspbian")) return OSKinds.Raspbian;

        if (osName.StartsWithIgnoreCase("Red Hat")) return OSKinds.RedHat;
        if (osName.StartsWithIgnoreCase("CentOS")) return OSKinds.CentOS;
        if (osName.StartsWithIgnoreCase("Fedora")) return OSKinds.Fedora;
        if (osName.StartsWithIgnoreCase("AlmaLinux")) return OSKinds.Alma;
        if (osName.StartsWithIgnoreCase("SUSE")) return OSKinds.SUSE;
        if (osName.StartsWithIgnoreCase("openSUSE")) return OSKinds.OpenSUSE;
        if (osName.Contains("SUSE")) return OSKinds.SUSE;
        if (osName.StartsWithIgnoreCase("Rocky")) return OSKinds.Rocky;

        if (osName.StartsWithIgnoreCase("Deepin")) return OSKinds.Deepin;
        if (osName.StartsWithIgnoreCase("UOS", "UnionTech OS")) return OSKinds.UOS;
        if (osName.StartsWithIgnoreCase("Kylin")) return OSKinds.Kylin;
        if (osName.StartsWithIgnoreCase("OpenKylin")) return OSKinds.OpenKylin;
        if (osName.StartsWithIgnoreCase("Loongnix")) return OSKinds.Loongnix;
        if (osName.StartsWithIgnoreCase("Red Flag")) return OSKinds.RedFlag;
        if (osName.StartsWithIgnoreCase("StartOS")) return OSKinds.StartOS;

        if (osName.StartsWithIgnoreCase("Alibaba")) return OSKinds.AlibabaLinux;
        if (osName.StartsWithIgnoreCase("NeoKylin")) return OSKinds.NeoKylin;
        if (osName.StartsWithIgnoreCase("Anolis")) return OSKinds.Anolis;
        if (osName.StartsWithIgnoreCase("Linx")) return OSKinds.Linx;
        if (osName.StartsWithIgnoreCase("openEuler")) return OSKinds.OpenEuler;
        if (osName.Contains("EulerOS")) return OSKinds.EulerOS;
        if (osName.StartsWithIgnoreCase("KylinSec")) return OSKinds.KylinSec;
        if (osName.StartsWithIgnoreCase("PuhuaOS")) return OSKinds.PuhuaOS;
        if (osName.StartsWithIgnoreCase("FangdeOS")) return OSKinds.FangdeOS;
        if (osName.StartsWithIgnoreCase("NewStartOS")) return OSKinds.NewStartOS;
        if (osName.StartsWithIgnoreCase("TencentOS")) return OSKinds.TencentOS;
        if (osName.StartsWithIgnoreCase("OpenCloudOS")) return OSKinds.OpenCloudOS;

        if (osName.Contains("OpenWrt")) return OSKinds.OpenWrt;
        if (osName.Contains("Buildroot")) return OSKinds.Buildroot;
        if (osName.Contains("Arch")) return OSKinds.ArchLinux;
        if (osName.Contains("Linux")) return OSKinds.Linux;

        if (osName.StartsWithIgnoreCase("Orange Pi"))
        {
            //if (osName.EndsWithIgnoreCase("Jammy")) return OSKinds.Ubuntu;
            //if (osName.EndsWithIgnoreCase("Bullseye")) return OSKinds.Debian;

            return OSKinds.Armbian;
        }

        if (Runtime.Linux) return OSKinds.Linux;

        return 0;
    }

    /// <summary>获取指定类型操作系统在指定架构上的运行时标识。如win-x64/linux-musl-arm64</summary>
    /// <param name="kind">系统类型</param>
    /// <param name="arch">芯片架构。小写</param>
    /// <returns></returns>
    public static RuntimeIdentifier[] GetRID(OSKinds kind, String arch)
    {
        var rid = RuntimeIdentifier.Any;
        if (kind >= OSKinds.MacOSX)
        {
            rid = arch switch
            {
                "x64" => RuntimeIdentifier.OsxX64,
                "arm64" => RuntimeIdentifier.OsxArm64,
                _ => RuntimeIdentifier.Osx,
            };
        }
        else if (kind >= OSKinds.Linux || kind == OSKinds.SmartOS)
        {
            rid = arch switch
            {
                "x64" => RuntimeIdentifier.LinuxX64,
                "arm" => RuntimeIdentifier.LinuxArm,
                "arm64" => RuntimeIdentifier.LinuxArm64,
                "mips64" => RuntimeIdentifier.LinuxMips64,
                "loongarch64" => RuntimeIdentifier.LinuxLA64,
                "riscv64" => RuntimeIdentifier.LinuxRiscV64,
                _ => RuntimeIdentifier.Linux,
            };
        }
        else if (kind >= OSKinds.Alpine)
        {
            rid = arch switch
            {
                "x64" => RuntimeIdentifier.LinuxMuslX64,
                "arm" => RuntimeIdentifier.LinuxMuslArm,
                "arm64" => RuntimeIdentifier.LinuxMuslArm64,
                _ => RuntimeIdentifier.LinuxMusl,
            };
        }
        else if (kind >= OSKinds.Win10)
        {
            rid = arch switch
            {
                "x86" => RuntimeIdentifier.WinX86,
                "x64" => RuntimeIdentifier.WinX64,
                "arm" => RuntimeIdentifier.WinArm,
                "arm64" => RuntimeIdentifier.WinArm64,
                _ => RuntimeIdentifier.Win,
            };
        }

        if (rid == RuntimeIdentifier.Any) return [rid];

        var ids = new List<RuntimeIdentifier>
        {
            rid
        };

        var rid2 = (RuntimeIdentifier)((Int32)rid / 10 * 10);
        if (rid2 != rid) ids.Add(rid2);

        return ids.ToArray();
    }
}