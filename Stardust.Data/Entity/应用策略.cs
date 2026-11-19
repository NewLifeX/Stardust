using System;
using Stardust.Models;

namespace Stardust.Data;

/// <summary>应用策略。把应用类型转换为具体的心跳周期、告警、激活、单例与埋点性能采集默认值</summary>
/// <remarks>Web 表示浏览器直接访问的 Web 服务端，Frontend 表示浏览器、WebView 或小程序中的前端；默认策略按类型生成。</remarks>
/// <remarks>
/// 应用类型只提供默认值，写入应用字段后由管理员在应用管理里自由调整。
/// 心跳性能数据始终采集，这里的 <see cref="EnableMeter"/> 只用于埋点上报是否携带性能信息。
/// </remarks>
public class AppPolicy
{
    /// <summary>有效应用类型</summary>
    public AppKinds Kind { get; set; }

    /// <summary>心跳周期。单位秒</summary>
    public Int32 Period { get; set; }

    /// <summary>下线告警</summary>
    public Boolean AlarmOnOffline { get; set; }

    /// <summary>自动激活服务</summary>
    public Boolean AutoActive { get; set; }

    /// <summary>单例部署</summary>
    public Boolean Singleton { get; set; }

    /// <summary>埋点上报是否携带性能信息。只影响埋点，不影响心跳</summary>
    public Boolean EnableMeter { get; set; }

    /// <summary>登录/心跳是否下发平台默认语言</summary>
    public Boolean SendDefaultLanguage { get; set; }
}
