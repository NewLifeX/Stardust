using System;
using System.ComponentModel;
using System.Reflection;
using Stardust.Data;
using Stardust.Models;
using Xunit;

namespace Stardust.Tests;

/// <summary>应用类型策略测试</summary>
public class AppPolicyTests
{
    [Fact(DisplayName = "每个应用类型都有唯一的中文描述，供管理端下拉显示")]
    public void KindsHaveUniqueDescriptions()
    {
        var labels = new HashSet<String>();
        foreach (var field in typeof(AppKinds).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            var label = field.GetCustomAttribute<DescriptionAttribute>()?.Description;
            Assert.False(String.IsNullOrEmpty(label), $"{field.Name} 缺少 Description");
            Assert.True(labels.Add(label), $"{field.Name} 的描述 [{label}] 与其他类型重复");
        }
    }

    [Fact(DisplayName = "各应用类型给出预期默认策略")]
    public void KindDefaults()
    {
        var server = AppKindPolicy.GetDefault(AppKinds.Server);
        Assert.Equal(60, server.Period);
        Assert.True(server.AlarmOnOffline);
        Assert.True(server.AutoActive);
        Assert.False(server.Singleton);
        Assert.True(server.EnableMeter);
        Assert.True(server.SendDefaultLanguage);

        var desktop = AppKindPolicy.GetDefault(AppKinds.Desktop);
        Assert.Equal(300, desktop.Period);
        Assert.False(desktop.AlarmOnOffline);
        Assert.False(desktop.AutoActive);
        Assert.True(desktop.Singleton);
        Assert.False(desktop.EnableMeter);
        Assert.False(desktop.SendDefaultLanguage);

        var mobile = AppKindPolicy.GetDefault(AppKinds.Mobile);
        Assert.Equal(300, mobile.Period);
        Assert.False(mobile.AlarmOnOffline);
        Assert.True(mobile.Singleton);
        Assert.False(mobile.EnableMeter);
        Assert.False(mobile.SendDefaultLanguage);

        var frontend = AppKindPolicy.GetDefault(AppKinds.Frontend);
        Assert.Equal(300, frontend.Period);
        Assert.False(frontend.AlarmOnOffline);
        Assert.False(frontend.Singleton);
        Assert.False(frontend.EnableMeter);
        Assert.False(frontend.SendDefaultLanguage);

        var web = AppKindPolicy.GetDefault(AppKinds.Web);
        Assert.Equal(60, web.Period);
        Assert.True(web.AlarmOnOffline);
        Assert.True(web.AutoActive);
        Assert.False(web.Singleton);
        Assert.True(web.EnableMeter);
        Assert.True(web.SendDefaultLanguage);
    }

    [Fact(DisplayName = "心跳周期 0 和 60 按类型生成，其它正数保留")]
    public void PeriodResolution()
    {
        var app = new App { Kind = AppKinds.Desktop, Period = 0 };
        Assert.Equal(300, AppKindPolicy.Resolve(app).Period);

        app.Period = 60;
        Assert.Equal(300, AppKindPolicy.Resolve(app).Period);

        app.Period = 120;
        Assert.Equal(120, AppKindPolicy.Resolve(app).Period);
    }

    [Fact(DisplayName = "管理员调整过的字段值直接生效")]
    public void ResolveUsesAppFields()
    {
        var app = new App
        {
            Kind = AppKinds.Server,
            Period = 120,
            AlarmOnOffline = false,
            AutoActive = false,
            Singleton = true,
        };

        var policy = AppKindPolicy.Resolve(app);
        Assert.Equal(120, policy.Period);
        Assert.False(policy.AlarmOnOffline);
        Assert.False(policy.AutoActive);
        Assert.True(policy.Singleton);
    }

    [Fact(DisplayName = "类型变化时沿用旧类型默认值的项切换到新类型默认值")]
    public void KindChangeFollowsNewDefaults()
    {
        // 从桌面应用默认值（300/不告警/不激活/单例）切到后端服务
        var app = new App
        {
            Kind = AppKinds.Server,
            Period = 300,
            AlarmOnOffline = false,
            AutoActive = false,
            Singleton = true,
        };

        AppKindPolicy.ChangeDefaults(app, AppKinds.Desktop);

        Assert.Equal(60, app.Period);
        Assert.True(app.AlarmOnOffline);
        Assert.True(app.AutoActive);
        Assert.False(app.Singleton);
    }

    [Fact(DisplayName = "类型变化时管理员调整过的值保留")]
    public void KindChangeKeepsCustomValues()
    {
        // 从后端服务切到桌面应用，但周期、告警、激活、单例都已被管理员调整
        var app = new App
        {
            Kind = AppKinds.Desktop,
            Period = 120,
            AlarmOnOffline = false,
            AutoActive = false,
            Singleton = true,
        };

        AppKindPolicy.ChangeDefaults(app, AppKinds.Server);

        Assert.Equal(120, app.Period);
        Assert.False(app.AlarmOnOffline);
        Assert.False(app.AutoActive);
        Assert.True(app.Singleton);
    }

    [Fact(DisplayName = "客户端首次上报类型时按新类型写入字段值")]
    public void FirstReportedKindFillsDefaults()
    {
        // 历史应用：类型未知、周期 0（按类型生成）、自动激活
        var app = new App
        {
            Kind = AppKinds.Desktop,
            Period = 0,
            AlarmOnOffline = false,
            AutoActive = true,
            Singleton = false,
        };

        AppKindPolicy.ChangeDefaults(app, AppKinds.Unknown);

        Assert.Equal(300, app.Period);
        Assert.False(app.AlarmOnOffline);
        Assert.False(app.AutoActive);
        Assert.True(app.Singleton);

        // 管理员调整过的周期保留
        app = new App
        {
            Kind = AppKinds.Desktop,
            Period = 120,
        };

        AppKindPolicy.ChangeDefaults(app, AppKinds.Unknown);

        Assert.Equal(120, app.Period);
    }

    [Fact(DisplayName = "未知类型保持字段原值")]
    public void UnknownKeepsLegacyValues()
    {
        var app = new App
        {
            Kind = AppKinds.Unknown,
            Period = 120,
            AlarmOnOffline = true,
            AutoActive = false,
            Singleton = true,
        };

        var policy = AppKindPolicy.Resolve(app);
        Assert.Equal(120, policy.Period);
        Assert.True(policy.AlarmOnOffline);
        Assert.False(policy.AutoActive);
        Assert.True(policy.Singleton);
        Assert.True(policy.SendDefaultLanguage);
    }

    [Fact(DisplayName = "新建应用的类型默认值不覆盖管理员显式字段")]
    public void ApplyDefaultsKeepsExplicitValues()
    {
        var app = new App
        {
            Kind = AppKinds.Desktop,
            AlarmOnOffline = true,
        };

        AppKindPolicy.ApplyDefaults(app);

        Assert.Equal(300, app.Period);
        Assert.True(app.AlarmOnOffline);
        Assert.False(app.AutoActive);
        Assert.True(app.Singleton);
    }

    [Fact(DisplayName = "节点代理按服务端登记，默认策略按代理处理")]
    public void NodeAgentUsesServerKindWithAgentDefaults()
    {
        var app = new App { Name = "StarAgent", Kind = AppKinds.Server };

        var policy = AppKindPolicy.GetDefault(app);
        Assert.Equal(AppKinds.Server, policy.Kind);
        Assert.Equal(300, policy.Period);
        Assert.False(policy.AlarmOnOffline);
        Assert.False(policy.AutoActive);
        Assert.True(policy.Singleton);
        Assert.False(policy.EnableMeter);
        Assert.True(policy.SendDefaultLanguage);

        // 普通服务端不受影响
        Assert.True(AppKindPolicy.GetDefault(new App { Name = "OrderApi", Kind = AppKinds.Server }).AlarmOnOffline);

        // 新建应用时同样写入代理默认值
        var created = new App { Name = "StarAgent", Kind = AppKinds.Server };
        AppKindPolicy.ApplyDefaults(created);
        Assert.Equal(300, created.Period);
        Assert.False(created.AlarmOnOffline);
        Assert.True(created.Singleton);
    }

    [Fact(DisplayName = "桌面、移动与前端不下发平台默认语言")]
    public void LanguageIsNotSentToClientKinds()
    {
        Assert.False(AppKindPolicy.ShouldSendDefaultLanguage(new App { Kind = AppKinds.Desktop }));
        Assert.False(AppKindPolicy.ShouldSendDefaultLanguage(new App { Kind = AppKinds.Mobile }));
        Assert.False(AppKindPolicy.ShouldSendDefaultLanguage(new App { Kind = AppKinds.Frontend }));
        Assert.True(AppKindPolicy.ShouldSendDefaultLanguage(new App { Kind = AppKinds.Server }));
        Assert.True(AppKindPolicy.ShouldSendDefaultLanguage(new App { Kind = AppKinds.Web }));
        Assert.True(AppKindPolicy.ShouldSendDefaultLanguage(new App { Name = "StarAgent", Kind = AppKinds.Server }));
        Assert.True(AppKindPolicy.ShouldSendDefaultLanguage(new App()));
    }

    [Fact(DisplayName = "在线实例超过目标时按稳定哈希采样")]
    public void SamplingIsStableAndCapped()
    {
        var kept = 0;
        for (var i = 0; i < 10_000; i++)
        {
            if (AppMeter.ShouldSample(1, "client-" + i, 10_000, 1_000)) kept++;
        }

        Assert.InRange(kept, 900, 1_100);

        // 同一实例多次判断结果一致
        Assert.Equal(
            AppMeter.ShouldSample(1, "client-fixed", 10_000, 1_000),
            AppMeter.ShouldSample(1, "client-fixed", 10_000, 1_000));
    }
}
