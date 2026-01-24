namespace Stardust.Models;

/// <summary>系统种类。主流操作系统类型，不考虑子版本</summary>
/// <remarks>
/// 用于大方向辨别能否运行某软件或执行某些操作，以及资产管理分类。
/// 精确系统信息请使用 Node 节点表的系统名称和版本号字段。
/// </remarks>
public enum OSKinds
{
    /// <summary>未知系统</summary>
    Unknown = 0,

    /// <summary>SmartOS。新生命团队自研嵌入式系统</summary>
    SmartOS = 40,

    #region Windows
    /// <summary>WinXP。经典桌面系统，服役13年(2001-2014)，仍有工控设备在用</summary>
    WinXP = 51,

    ///// <summary>WinXP SP3。补丁版本，合并到 WinXP</summary>
    //WinXP3 = 53,

    ///// <summary>Win2003。无法运行 .NET 4.5+，已淘汰</summary>
    //Win2003 = 52,

    ///// <summary>Vista。失败的过渡版本，用户量极少，合并到 Win7</summary>
    //WinVista = 60,

    /// <summary>Win2008。最后支持 XP 时代驱动的服务器，部分遗留系统仍在用</summary>
    Win2008 = 68,

    /// <summary>Win7。最成功的桌面系统之一，服役11年(2009-2020)，仍有大量用户</summary>
    Win7 = 61,

    ///// <summary>Win7 SP1。补丁版本，合并到 Win7</summary>
    //Win71 = 67,

    ///// <summary>Win8。失败的触屏优先设计，用户量极少，合并到 Win7</summary>
    //Win8 = 62,

    ///// <summary>Win8.1。补丁版本，合并到 Win7</summary>
    //Win81 = 63,

    /// <summary>Win2012。首个支持 Docker 的 Windows Server，云计算过渡期常见</summary>
    Win2012 = 64,

    /// <summary>Win2016。长期支持版本，企业服务器主流选择</summary>
    Win2016 = 66,

    /// <summary>Win2019。容器化支持成熟，当前企业主流版本</summary>
    Win2019 = 69,

    /// <summary>Win2022。最新长期支持版，支持 Azure 混合云</summary>
    Win2022 = 72,

    /// <summary>Windows Server 通用。无法识别具体版本时的兜底分类</summary>
    WinServer = 70,

    /// <summary>Win10。当前桌面主流，持续更新至2025年</summary>
    Win10 = 10,

    /// <summary>Win11。最新桌面系统，TPM 2.0 硬件要求</summary>
    Win11 = 11,
    #endregion

    /// <summary>Alpine。轻量级容器首选，镜像仅5MB，使用 musl 库</summary>
    Alpine = 90,

    /// <summary>Linux 通用。无法识别具体发行版时的兜底分类</summary>
    Linux = 100,

    /// <summary>ArchLinux。滚动更新，开发者和极客偏爱</summary>
    ArchLinux = 101,

    /// <summary>OpenWrt。路由器/IoT 网关主流系统</summary>
    OpenWrt = 102,

    /// <summary>Buildroot。嵌入式 Linux 构建系统，工控设备常见</summary>
    Buildroot = 103,

    #region Debian系
    /// <summary>Ubuntu。全球最流行的 Linux 桌面/服务器发行版</summary>
    Ubuntu = 110,

    /// <summary>Debian。稳定性著称，Ubuntu/Deepin 等的上游</summary>
    Debian = 111,

    /// <summary>Armbian。ARM 开发板专用，Orange Pi/Banana Pi 等常用</summary>
    Armbian = 112,

    /// <summary>Raspbian。树莓派官方系统，教育和 IoT 领域广泛使用</summary>
    Raspbian = 113,
    #endregion

    #region RedHat系
    /// <summary>RHEL。企业级 Linux 标杆，付费订阅，金融/电信行业首选</summary>
    RedHat = 120,

    /// <summary>CentOS。RHEL 免费替代品，曾是服务器最流行发行版</summary>
    CentOS = 121,

    /// <summary>Fedora。RHEL 上游试验田，新技术先行者</summary>
    Fedora = 122,

    /// <summary>AlmaLinux。CentOS 停更后的社区替代品，1:1 兼容 RHEL</summary>
    Alma = 123,

    ///// <summary>SUSE。欧洲企业市场为主，国内少见，归类到 RedHat 系</summary>
    //SUSE = 124,

    ///// <summary>openSUSE。社区版 SUSE，国内少见，归类到 RedHat 系</summary>
    //OpenSUSE = 125,

    /// <summary>Rocky Linux。CentOS 创始人新作，企业级稳定性保证</summary>
    Rocky = 126,
    #endregion

    #region 国产Linux
    /// <summary>Deepin。国产桌面体验最佳，DDE 桌面环境广受好评</summary>
    Deepin = 130,

    /// <summary>UOS。统信软件出品，政企桌面市场占有率第一</summary>
    UOS = 131,

    /// <summary>Kylin。银河麒麟，国防/政务核心系统指定</summary>
    Kylin = 132,

    ///// <summary>OpenKylin。社区版麒麟，用户量少，归类到 Kylin</summary>
    //OpenKylin = 133,

    ///// <summary>Loongnix。龙芯专用，硬件绑定，归类到 Debian</summary>
    //Loongnix = 134,

    ///// <summary>红旗 Linux。曾经的国产先驱，已式微，归类到 Debian</summary>
    //RedFlag = 135,

    ///// <summary>StartOS。冷门发行版，归类到 Debian</summary>
    //StartOS = 136,

    /// <summary>Alibaba Cloud Linux。阿里云默认系统，针对云场景深度优化</summary>
    AlibabaLinux = 140,

    ///// <summary>NeoKylin。中标麒麟，与银河麒麟合并，归类到 CentOS</summary>
    //NeoKylin = 141,

    /// <summary>Anolis。阿里主导开源社区，CentOS 国产替代方案</summary>
    Anolis = 142,

    ///// <summary>Linx。凝思安全 OS，军工/涉密专用，用户量极少</summary>
    //Linx = 143,

    /// <summary>openEuler。华为开源，鲲鹏生态核心，国产服务器新势力</summary>
    OpenEuler = 144,

    ///// <summary>EulerOS。华为商业版，合并到 openEuler</summary>
    //EulerOS = 145,

    ///// <summary>KylinSec。麒麟信安，细分市场，归类到 CentOS</summary>
    //KylinSec = 146,

    ///// <summary>PuhuaOS。普华，电力行业为主，归类到 CentOS</summary>
    //PuhuaOS = 147,

    ///// <summary>FangdeOS。方德，教育行业为主，归类到 CentOS</summary>
    //FangdeOS = 148,

    ///// <summary>NewStartOS。新支点，政务为主，归类到 CentOS</summary>
    //NewStartOS = 149,

    /// <summary>TencentOS。腾讯云默认系统，针对云原生场景优化</summary>
    TencentOS = 150,

    ///// <summary>OpenCloudOS。腾讯主导开源社区，合并到 TencentOS</summary>
    //OpenCloudOS = 151,

    ///// <summary>LoongOS。龙芯嵌入式实时 OS，硬件绑定，用户量极少</summary>
    //LoongOS = 160,
    #endregion

    /// <summary>macOS。苹果桌面系统，开发者群体重要平台</summary>
    MacOSX = 400,

    /// <summary>Android。全球第一大移动操作系统</summary>
    Android = 500,
}