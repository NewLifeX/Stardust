using System;
using NewLife;
using Stardust.Data.Monitors;
using Stardust.Models;

namespace Stardust.Data;

/// <summary>应用类型默认策略。把类型转换为默认值，并在新建或类型变化时写入应用字段</summary>
/// <remarks>
/// 类型只提供一份默认值，写入应用字段后由管理员在应用管理里自由调整，不额外保存覆盖记录。
/// <see cref="AppKinds.Server"/> 与 <see cref="AppKinds.Web"/> 的默认策略相同，Web 仅表示浏览器直接访问的 Web 服务端；
/// 节点代理（StarAgent）按 <see cref="AppKinds.Server"/> 登记，平台按应用代码识别后套用代理默认值。
/// </remarks>
public static class AppKindPolicy
{
    /// <summary>节点代理的应用代码。平台自身组件，按服务端形态登记但策略按代理处理</summary>
    private const String NodeAgentName = "StarAgent";

    /// <summary>获取指定类型的默认策略</summary>
    /// <param name="kind">应用类型</param>
    /// <returns>默认策略</returns>
    public static AppPolicy GetDefault(AppKinds kind) => kind switch
    {
        AppKinds.Server => new AppPolicy
        {
            Kind = kind,
            Period = 60,
            AlarmOnOffline = true,
            AutoActive = true,
            Singleton = false,
            EnableMeter = true,
            SendDefaultLanguage = true,
        },
        AppKinds.Web => new AppPolicy
        {
            Kind = kind,
            Period = 60,
            AlarmOnOffline = true,
            AutoActive = true,
            Singleton = false,
            EnableMeter = true,
            SendDefaultLanguage = true,
        },
        AppKinds.Desktop => new AppPolicy
        {
            Kind = kind,
            Period = 300,
            AlarmOnOffline = false,
            AutoActive = false,
            Singleton = true,
            EnableMeter = false,
            SendDefaultLanguage = false,
        },
        AppKinds.Mobile => new AppPolicy
        {
            Kind = kind,
            Period = 300,
            AlarmOnOffline = false,
            AutoActive = false,
            Singleton = true,
            EnableMeter = false,
            SendDefaultLanguage = false,
        },
        AppKinds.Frontend => new AppPolicy
        {
            Kind = kind,
            Period = 300,
            AlarmOnOffline = false,
            AutoActive = false,
            Singleton = false,
            EnableMeter = false,
            SendDefaultLanguage = false,
        },
        _ => new AppPolicy
        {
            Kind = AppKinds.Unknown,
            Period = 60,
            AlarmOnOffline = false,
            AutoActive = true,
            Singleton = false,
            EnableMeter = true,
            SendDefaultLanguage = true,
        },
    };

    /// <summary>获取应用的默认策略。节点代理按服务端形态登记，但套用代理默认值</summary>
    /// <param name="app">应用</param>
    /// <returns>默认策略</returns>
    public static AppPolicy GetDefault(App app) => GetDefault(app, app?.Kind ?? AppKinds.Unknown);

    /// <summary>获取应用在指定形态下的默认策略</summary>
    /// <param name="app">应用</param>
    /// <param name="kind">应用形态</param>
    /// <returns>默认策略</returns>
    private static AppPolicy GetDefault(App app, AppKinds kind)
    {
        if (app != null && kind == AppKinds.Server && app.Name.EqualIgnoreCase(NodeAgentName))
        {
            // 节点代理不产生下线告警与自动激活，每机单实例，且不下发埋点性能
            return new AppPolicy
            {
                Kind = kind,
                Period = 300,
                AlarmOnOffline = false,
                AutoActive = false,
                Singleton = true,
                EnableMeter = false,
                SendDefaultLanguage = true,
            };
        }

        return GetDefault(kind);
    }

    /// <summary>判断是否向客户端下发平台默认语言</summary>
    /// <param name="app">应用</param>
    /// <returns>是否下发</returns>
    public static Boolean ShouldSendDefaultLanguage(App app) => GetDefault(app).SendDefaultLanguage;

    /// <summary>解析应用的有效策略</summary>
    /// <param name="app">应用</param>
    /// <param name="tracer">应用跟踪器。为空时埋点性能采集按类型默认值</param>
    /// <returns>有效策略</returns>
    public static AppPolicy Resolve(App app, AppTracer tracer = null)
    {
        if (app == null) throw new ArgumentNullException(nameof(app));

        var kind = app.Kind;
        var def = GetDefault(app);
        return new AppPolicy
        {
            Kind = kind,
            Period = ResolveLegacyPeriod(app.Period, def.Period),
            AlarmOnOffline = app.AlarmOnOffline,
            AutoActive = app.AutoActive,
            Singleton = app.Singleton,
            EnableMeter = tracer?.EnableMeter ?? def.EnableMeter,
            SendDefaultLanguage = def.SendDefaultLanguage,
        };
    }

    /// <summary>解析心跳周期。0 和 60 表示按类型生成</summary>
    /// <param name="period">字段原值</param>
    /// <param name="defaultPeriod">类型默认周期</param>
    /// <returns>有效周期</returns>
    public static Int32 ResolveLegacyPeriod(Int32 period, Int32 defaultPeriod)
        => period > 0 && period != 60 ? period : defaultPeriod;

    /// <summary>新建应用时写入类型默认值。管理员显式提交的字段优先</summary>
    /// <param name="app">应用</param>
    public static void ApplyDefaults(App app)
    {
        if (app == null) return;

        var kind = app.Kind;
        if (kind == AppKinds.Unknown)
        {
            // 未知类型保持历史行为：新应用默认自动激活
            if (!app.IsFieldDirty(nameof(App.AutoActive))) app.AutoActive = true;
            return;
        }

        var def = GetDefault(app);
        if (!app.IsFieldDirty(nameof(App.Period))) app.Period = def.Period;
        if (!app.IsFieldDirty(nameof(App.AlarmOnOffline))) app.AlarmOnOffline = def.AlarmOnOffline;
        if (!app.IsFieldDirty(nameof(App.AutoActive))) app.AutoActive = def.AutoActive;
        if (!app.IsFieldDirty(nameof(App.Singleton))) app.Singleton = def.Singleton;
    }

    /// <summary>应用类型变化时更新策略字段。仍等于旧类型默认值的项切换到新类型默认值，管理员调整过的值保留</summary>
    /// <param name="app">应用</param>
    /// <param name="oldKind">变化前的应用类型</param>
    public static void ChangeDefaults(App app, AppKinds oldKind)
    {
        if (app == null) return;

        var oldDef = GetDefault(app, oldKind);
        var newDef = GetDefault(app);

        if (ResolveLegacyPeriod(app.Period, oldDef.Period) == oldDef.Period) app.Period = newDef.Period;
        if (app.AlarmOnOffline == oldDef.AlarmOnOffline) app.AlarmOnOffline = newDef.AlarmOnOffline;
        if (app.AutoActive == oldDef.AutoActive) app.AutoActive = newDef.AutoActive;
        if (app.Singleton == oldDef.Singleton) app.Singleton = newDef.Singleton;
    }
}
