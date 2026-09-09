using System.ComponentModel;

namespace Stardust.Models;

/// <summary>重注册模式。已存在应用的动态注册策略</summary>
/// <remarks>
/// 客户端（应用）以空密钥或错误密钥登录时，若应用已存在，默认拒绝复用旧身份，防止身份冒用与真实密钥泄露。
/// 该字段控制允许动态注册（自动接入领取密钥）的策略：
/// - Off：关闭，一律拒绝（默认，安全）
/// - Auto：收敛窗口，临时放行，首个成功登录后自动关闭，用于引导新实例临时自动接入
/// - Trusted：可信接入，仅同项目受管节点（NodeCode），或UUID/Macs命中该应用近期在线指纹的机器可自助接入（平台认识的机器）
/// - Open：开放，仅凭应用代码即可自助接入（低门槛，身份门槛=应用代码）
/// 
/// 重注册始终复用既有身份、不轮换密钥，避免把真实实例踢下线。
/// </remarks>
public enum ReRegisterModes
{
    /// <summary>关闭。禁止已存在应用动态注册复用旧身份（默认，安全）</summary>
    [Description("关闭")]
    Off = 0,

    /// <summary>收敛窗口。允许新实例临时自助接入领取密钥，首个成功登录后自动关闭（引导期使用）</summary>
    [Description("收敛窗口")]
    Auto = 1,

    /// <summary>可信接入。同项目受管节点（NodeCode），或UUID/Macs命中该应用近期指纹的机器可自助接入（长期，平台认识的机器）</summary>
    [Description("可信接入")]
    Trusted = 2,

    /// <summary>开放。仅凭应用代码即可自助接入领取密钥（长期无门槛，身份门槛=应用代码，仅建议低风险应用）</summary>
    [Description("开放")]
    Open = 3,
}
