using System.ComponentModel;

namespace Stardust.Models;

/// <summary>重注册模式。已存在应用的动态注册策略</summary>
/// <remarks>
/// 客户端（应用）以空密钥或错误密钥登录时，若应用已存在，默认拒绝复用旧身份，防止身份冒用与真实密钥泄露。
/// 该字段控制允许动态注册（自动接入领取密钥）的策略，取值从宽到严：
/// - Default(0)：默认，跟随服务端全局默认策略（平台设置中的重注册模式），应用无需逐个配置
/// - Open(1)：开放，仅凭应用代码即可自助接入领取密钥（最宽松，仅建议低风险应用）
/// - Trusted(2)：可信接入，仅同项目受管节点（NodeCode），或UUID/Macs命中该应用近期在线指纹的机器可自助接入
/// - Temporary(3)：临时放行，首次成功登录后自动关闭，用于引导新实例或密钥丢失后恢复（引导期使用）
/// - Off(4)：关闭，一律拒绝（最严格）
/// 
/// 取值兼容：0 在历史版本表示关闭，现改为默认跟随全局，存量应用无需数据迁移；
/// 1/2/3/4 为新顺序，历史显式配置过的应用（如开放/临时放行）升级后需在应用里按新名称重新选择一次。
/// 
/// 重注册始终复用既有身份、不轮换密钥，避免把真实实例踢下线。
/// 应用登录（App/Login）与令牌接口（OAuth令牌、配置中心、网关路由）均遵循该策略，其中可信接入暂不适用于令牌接口。
/// </remarks>
public enum ReRegisterModes
{
    /// <summary>默认。跟随服务端全局默认策略（平台设置中的重注册模式）。历史应用存量值即 0，无需迁移</summary>
    [Description("默认")]
    Default = 0,

    /// <summary>开放。仅凭应用代码即可自助接入领取密钥（最宽松，仅建议低风险应用）</summary>
    [Description("开放")]
    Open = 1,

    /// <summary>可信接入。同项目受管节点（NodeCode），或UUID/Macs命中该应用近期指纹的机器可自助接入（较严格）</summary>
    [Description("可信接入")]
    Trusted = 2,

    /// <summary>临时放行。客户端携错密钥时临时放行，首次成功登录后自动关闭（引导新实例或密钥丢失恢复）</summary>
    [Description("临时放行")]
    Temporary = 3,

    /// <summary>关闭。禁止已存在应用动态注册复用旧身份（最严格）</summary>
    [Description("关闭")]
    Off = 4,
}

/// <summary>重注册模式辅助</summary>
public static class ReRegisterModesHelper
{
    /// <summary>解析实际生效的重注册模式。应用配置为“默认”时跟随服务端全局默认</summary>
    /// <param name="mode">应用配置的重注册模式</param>
    /// <param name="global">服务端全局默认重注册模式</param>
    /// <returns>实际生效的重注册模式。全局默认也为“默认”时按关闭处理</returns>
    public static ReRegisterModes Resolve(this ReRegisterModes mode, ReRegisterModes global)
    {
        if (mode != ReRegisterModes.Default) return mode;

        return global == ReRegisterModes.Default ? ReRegisterModes.Off : global;
    }
}
