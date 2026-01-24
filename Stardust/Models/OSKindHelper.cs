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

        // XP 系列统一归类（含 SP3）
        if (osName.StartsWithIgnoreCase("Windows XP")) return OSKinds.WinXP;
        if (osName.StartsWithIgnoreCase("Windows Server 2003")) return OSKinds.WinXP;

        // Win8/Win8.1/Vista 统一归类为 Win7（失败版本，用户量极少）
        if (osName.StartsWithIgnoreCase("Windows 8")) return OSKinds.Win7;
        if (osName.StartsWithIgnoreCase("Windows 7")) return OSKinds.Win7;
        if (osName.StartsWithIgnoreCase("Windows Vista")) return OSKinds.Win7;

        // 优先识别新系统
        if (osName.StartsWithIgnoreCase("Windows 11")) return OSKinds.Win11;
        if (osName.StartsWithIgnoreCase("Windows 10")) return OSKinds.Win10;

        if (osName.StartsWithIgnoreCase("Windows Server 2022")) return OSKinds.Win2022;
        if (osName.StartsWithIgnoreCase("Windows Server 2019")) return OSKinds.Win2019;
        if (osName.StartsWithIgnoreCase("Windows Server 2016")) return OSKinds.Win2016;
        if (osName.StartsWithIgnoreCase("Windows Server 2012")) return OSKinds.Win2012;
        if (osName.StartsWithIgnoreCase("Windows Server 2008")) return OSKinds.Win2008;
        // 旧服务器版本统一归类为 WinServer
        if (osName.StartsWithIgnoreCase("Windows Server")) return OSKinds.WinServer;

        // 根据版本号识别
        var str = osVersion.Length < 3 ? osVersion : osVersion.Substring(0, 3);
        if (osName.Contains("Server"))
        {
            if (str.StartsWith("5.")) return OSKinds.WinXP;
            if (str.StartsWith("6.")) return OSKinds.Win2008;

            return OSKinds.WinServer;
        }

        return str switch
        {
            "10." => OSKinds.Win10,
            "6.3" or "6.2" or "6.1" or "6.0" => OSKinds.Win7,  // Vista/Win7/Win8 系列
            "5.2" or "5.1" => OSKinds.WinXP,
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

        // 优先识别特殊系统
        if (osName.StartsWithIgnoreCase("Alpine") || osName.EndsWithIgnoreCase("(Alpine)")) return OSKinds.Alpine;
        if (osName.StartsWithIgnoreCase("Arch")) return OSKinds.ArchLinux;

        // Debian 系
        if (osName.StartsWithIgnoreCase("Ubuntu")) return OSKinds.Ubuntu;
        if (osName.StartsWithIgnoreCase("Debian")) return OSKinds.Debian;
        if (osName.Contains("Armbian")) return OSKinds.Armbian;
        if (osName.StartsWithIgnoreCase("Raspbian")) return OSKinds.Raspbian;

        // RedHat 系
        if (osName.StartsWithIgnoreCase("Red Hat")) return OSKinds.RedHat;
        if (osName.StartsWithIgnoreCase("CentOS")) return OSKinds.CentOS;
        if (osName.StartsWithIgnoreCase("Fedora")) return OSKinds.Fedora;
        if (osName.StartsWithIgnoreCase("AlmaLinux")) return OSKinds.Alma;
        if (osName.StartsWithIgnoreCase("Rocky")) return OSKinds.Rocky;
        // SUSE 系归类为 RedHat（RPM 包管理）
        if (osName.Contains("SUSE")) return OSKinds.RedHat;

        // 国产主流（DEB 系）
        if (osName.StartsWithIgnoreCase("Deepin")) return OSKinds.Deepin;
        if (osName.StartsWithIgnoreCase("UOS", "UnionTech OS")) return OSKinds.UOS;
        if (osName.StartsWithIgnoreCase("Kylin", "OpenKylin")) return OSKinds.Kylin;
        // 冷门 DEB 系归类为 Debian
        if (osName.StartsWithIgnoreCase("Loongnix", "Red Flag", "StartOS")) return OSKinds.Debian;

        // 国产主流（RPM 系）
        if (osName.StartsWithIgnoreCase("Alibaba")) return OSKinds.AlibabaLinux;
        if (osName.StartsWithIgnoreCase("Anolis")) return OSKinds.Anolis;
        if (osName.StartsWithIgnoreCase("openEuler") || osName.Contains("EulerOS")) return OSKinds.OpenEuler;
        if (osName.StartsWithIgnoreCase("TencentOS", "OpenCloudOS")) return OSKinds.TencentOS;
        // 冷门 RPM 系归类为 CentOS
        if (osName.StartsWithIgnoreCase("NeoKylin", "Linx", "KylinSec", "PuhuaOS", "FangdeOS", "NewStartOS")) return OSKinds.CentOS;

        // 嵌入式/路由器系统
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