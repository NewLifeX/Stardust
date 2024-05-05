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

        if (osName.Contains("Mac")) return OSKinds.MacOSX;
        if (osName.Contains("Android")) return OSKinds.Android;

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

        if (osName.StartsWithIgnoreCase("Windows 7")) return osVersion.StartsWith("6.1.7601") ? OSKinds.Win71 : OSKinds.Win7;

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
        if (osName.StartsWithIgnoreCase("Ubuntu")) return OSKinds.Ubuntu;
        if (osName.StartsWithIgnoreCase("Debian")) return OSKinds.Debian;
        if (osName.StartsWithIgnoreCase("Deepin")) return OSKinds.Deepin;
        if (osName.StartsWithIgnoreCase("Raspbian")) return OSKinds.Raspbian;

        if (osName.StartsWithIgnoreCase("Red Hat")) return OSKinds.RedHat;
        if (osName.StartsWithIgnoreCase("CentOS")) return OSKinds.CentOS;
        if (osName.StartsWithIgnoreCase("Alibaba")) return OSKinds.AlibabaLinux;

        if (osName.StartsWithIgnoreCase("UOS", "UnionTech OS")) return OSKinds.UOS;
        if (osName.StartsWithIgnoreCase("Kylin", "NeoKylin")) return OSKinds.Kylin;
        if (osName.StartsWithIgnoreCase("OpenKylin")) return OSKinds.OpenKylin;
        if (osName.StartsWithIgnoreCase("Linx")) return OSKinds.Linx;
        if (osName.StartsWithIgnoreCase("openEuler")) return OSKinds.OpenEuler;

        if (osName.Contains("Linux")) return OSKinds.Linux;
        if (osName.Contains("Buildroot")) return OSKinds.Linux;
        if (osName.Contains("OpenWrt")) return OSKinds.Linux;
        if (osName.Contains("Armbian")) return OSKinds.Debian;

        if (osName.StartsWithIgnoreCase("Orange Pi"))
        {
            if (osName.EndsWithIgnoreCase("Jammy")) return OSKinds.Ubuntu;
            if (osName.EndsWithIgnoreCase("Bullseye")) return OSKinds.Debian;

            return OSKinds.Linux;
        }

        return 0;
    }
}