using System.ComponentModel;

namespace Stardust.Models;

/// <summary>应用类型。描述应用呈现的形态，不表示部署位置：本地运行 Web 后端同样是 Web</summary>
/// <remarks>
/// 类型只提供默认策略（心跳周期、离线告警、服务激活、单例、埋点性能采集、平台语言下发），
/// 新建应用或类型变化时写入应用字段，之后可在应用管理里单独调整。
/// 服务端侧使用 <see cref="Server"/>（通用服务端）与 <see cref="Web"/>（浏览器直接访问的 Web 应用后端），两者默认策略相同，仅画像不同；
/// 客户端侧使用 <see cref="Desktop"/>（带本地界面）、<see cref="Mobile"/>（移动应用）与 <see cref="Frontend"/>（浏览器/WebView 前端静态包）。
/// 节点代理（StarAgent）按 <see cref="Server"/> 登记，由平台识别后按代理策略处理，不单列为类型。
/// </remarks>
public enum AppKinds
{
    /// <summary>未知。历史应用默认值，保持既有行为</summary>
    [Description("未知")]
    Unknown = 0,

    /// <summary>服务端。无本地界面的通用服务端，如 Worker、任务宿主、控制台后台服务</summary>
    [Description("服务端")]
    Server = 1,

    /// <summary>Web 服务端。Web 应用的后端，如 ASP.NET Core、Node.js、Java Web；本地运行同样归此类，默认策略与服务端相同</summary>
    [Description("Web 服务端")]
    Web = 2,

    /// <summary>桌面应用。带本地界面的桌面客户端，如 WinForms、WPF、Avalonia</summary>
    [Description("桌面应用")]
    Desktop = 3,

    /// <summary>移动应用。运行在手机或平板上的本地应用，不区分 Android/iOS</summary>
    [Description("移动应用")]
    Mobile = 4,

    /// <summary>前端。浏览器、移动端 WebView 或小程序中的 JS/TS 前端，发布中心发布其静态包</summary>
    [Description("前端")]
    Frontend = 5,
}
