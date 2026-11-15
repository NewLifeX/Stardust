using System;
using System.Globalization;
using System.Linq;
using System.Reflection;
using NewLife;
using NewLife.Remoting.Services;
using Stardust.Data;
using Xunit;

namespace Stardust.Tests;

/// <summary>历史操作名本地化测试。验证库中保存的英文规范键可按界面语言翻译；旧版中文数据不做兼容</summary>
public class HistoryActionTests : IDisposable
{
    private readonly CultureInfo? _old = Localization.Culture;

    /// <summary>还原界面语言</summary>
    public void Dispose() => Localization.Culture = _old;

    [Fact(DisplayName = "历史操作名按界面语言翻译")]
    public void LocalizeByCulture()
    {
        Localization.Culture = CultureInfo.GetCultureInfo("zh-Hans");

        Assert.Equal("应用鉴权", HistoryAction.Localize(HistoryAction.ApplicationAuthentication));
        Assert.Equal("WebSocket 连接", HistoryAction.Localize("WebSocket connected"));

        Localization.Culture = CultureInfo.GetCultureInfo("en");

        Assert.Equal(HistoryAction.ApplicationAuthentication, HistoryAction.Localize(HistoryAction.ApplicationAuthentication));
    }

    [Fact(DisplayName = "全部操作常量均提供简体中文译文")]
    public void AllConstantsTranslated()
    {
        Localization.Culture = CultureInfo.GetCultureInfo("zh-Hans");

        var fields = typeof(HistoryAction).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(e => e.IsLiteral && e.FieldType == typeof(String))
            .ToArray();
        Assert.NotEmpty(fields);

        foreach (var field in fields)
        {
            var key = (String)field.GetRawConstantValue()!;
            Assert.NotEqual(key, HistoryAction.Localize(key));
        }
    }

    [Fact(DisplayName = "变更动作只包含生命周期与升级类操作")]
    public void ChangeActionFilter()
    {
        Assert.True(HistoryAction.IsChange(HistoryAction.ApplicationRegistered));
        Assert.False(HistoryAction.IsChange(HistoryAction.ApplicationAuthentication));
        Assert.False(HistoryAction.IsChange(HistoryAction.WebSocketConnected));
    }

    [Fact(DisplayName = "未知操作名原样返回")]
    public void UnknownActionKeepsText()
    {
        Localization.Culture = CultureInfo.GetCultureInfo("zh-Hans");

        Assert.Equal("MyCustomAction", HistoryAction.Localize("MyCustomAction"));
        Assert.Equal("", HistoryAction.Localize(""));
        Assert.Null(HistoryAction.Localize(null));
    }

    [Fact(DisplayName = "历史动作常量与 Remoting 规范键一致")]
    public void MatchesRemotingDeviceActions()
    {
        // 同一事件由框架与星尘共同写入时，取值必须一致，否则库里会出现两种规范键
        Assert.Equal(DeviceActions.DeviceOffline, HistoryAction.DeviceOffline);
        Assert.Equal(DeviceActions.OfflineTimeout, HistoryAction.OfflineTimeout);
        Assert.Equal(DeviceActions.DynamicRegistration, HistoryAction.DynamicRegistration);
        Assert.Equal(DeviceActions.IdentityMismatch, HistoryAction.IdentityMismatch);
        Assert.Equal(DeviceActions.DeviceEvent, HistoryAction.DeviceEvent);
    }
}
