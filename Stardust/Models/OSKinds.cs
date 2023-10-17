namespace Stardust.Models;

/// <summary>系统种类。主流操作系统类型，不考虑子版本</summary>
public enum OSKinds
{
    /// <summary>未知</summary>
    Unknown = 0,

    /// <summary>SmartOS by NewLife</summary>
    SmartOS = 40,

    /// <summary>WinXP, 5.1.2600</summary>
    WinXP = 51,

    /// <summary>WinXP SP3, 5.1.2600</summary>
    WinXP3 = 53,

    /// <summary>WinXP, 5.2.3790</summary>
    Win2003 = 52,

    /// <summary>Vista, 6.0.6000</summary>
    WinVista = 60,

    /// <summary>Win2008, 6.0.6001</summary>
    Win2008 = 68,

    /// <summary>Win7, 6.1.7600</summary>
    Win7 = 61,

    /// <summary>Win7 SP1, 6.1.7601</summary>
    Win71 = 67,

    /// <summary>Win8, 6.2.9200</summary>
    Win8 = 62,

    /// <summary>Win8.1, 6.3.9200</summary>
    Win81 = 63,

    /// <summary>Win2012, 6.3.9600</summary>
    Win2012 = 64,

    /// <summary>Win2016</summary>
    Win2016 = 66,

    /// <summary>Win2019</summary>
    Win2019 = 69,

    /// <summary>Win10, 10.0.10240</summary>
    Win10 = 10,

    /// <summary>Win11, 10.0.22000</summary>
    Win11 = 11,

    /// <summary>Linux</summary>
    Linux = 100,

    /// <summary>Ubuntu</summary>
    Ubuntu = 110,

    /// <summary>Debian</summary>
    Debian = 111,

    /// <summary>深度</summary>
    Deepin = 112,

    /// <summary>树莓派</summary>
    Raspbian = 113,

    /// <summary>红帽</summary>
    RedHat = 120,

    /// <summary>Centos</summary>
    CentOS = 121,

    /// <summary>Alibaba Cloud Linux</summary>
    AlibabaLinux = 122,

    /// <summary>统信UOS</summary>
    UOS = 130,

    /// <summary>麒麟</summary>
    Kylin = 140,

    /// <summary>优麒麟</summary>
    OpenKylin = 141,

    /// <summary>MacOS</summary>
    MacOSX = 200,

    /// <summary>Android</summary>
    Android = 300,
}