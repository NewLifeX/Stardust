using System;
using System.Globalization;
using System.Linq;
using System.Reflection;
using NewLife;
using Stardust.Data;
using Xunit;

namespace Stardust.Tests;

/// <summary>历史操作名本地化测试。验证库中保存的英文规范键可按界面语言翻译，旧中文数据可归一化</summary>
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

        // 旧版本写入的中文操作名先归一化再翻译
        Assert.Equal("应用鉴权", HistoryAction.Localize("应用鉴权"));
        Assert.Equal("超时下线", HistoryAction.Localize("节点下线"));

        Localization.Culture = CultureInfo.GetCultureInfo("en");

        Assert.Equal(HistoryAction.ApplicationAuthentication, HistoryAction.Localize("应用鉴权"));
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
        Assert.True(HistoryAction.IsChange("应用注册"));
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
}
