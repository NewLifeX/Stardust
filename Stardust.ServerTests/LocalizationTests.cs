using System;
using System.Globalization;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using NewLife;
using Xunit;

namespace ServerTest;

/// <summary>数据层多语言词表测试。验证 31 种语言全部注册并可翻译</summary>
[Collection("Localization")]
public class LocalizationTests : IDisposable
{
    private static readonly String[] _locales =
    [
        "ar", "bn", "cs", "da", "de", "el", "es", "fa", "fi", "fr", "he", "hi", "hu", "id", "it", "ja", "ko",
        "ms", "nb", "nl", "pl", "pt", "ro", "ru", "sv", "th", "tr", "uk", "vi", "zh-Hans", "zh-Hant",
    ];

    private readonly CultureInfo? _old = Localization.Culture;

    /// <summary>恢复原始目标语言</summary>
    public void Dispose() => Localization.Culture = _old;

    [Fact(DisplayName = "星尘数据层的31种语言词表全部注册")]
    public void DataLocalesRegistered()
    {
        RuntimeHelpers.RunModuleConstructor(typeof(Stardust.Data.Configs.ConfigData).Module.ModuleHandle);

        Assert.Equal(31, _locales.Length);

        foreach (var code in _locales)
        {
            Localization.Culture = new CultureInfo(code);

            Assert.NotEqual("Action cannot be empty.", Localization.Translate("Action cannot be empty."));
            Assert.NotEqual("Domain cannot be empty.", Localization.Translate("Domain cannot be empty."));
        }

        Localization.Culture = new CultureInfo("zh-Hans");
        Assert.Equal("版本号不能为空", Localization.Translate("Version cannot be empty."));
        Assert.Equal("不能为空！", Localization.Translate("Cannot be empty."));
    }

    [Fact(DisplayName = "服务端词表注册并支持中文翻译")]
    public void ServerLocalesRegistered()
    {
        RuntimeHelpers.RunModuleConstructor(typeof(global::Stardust.Server.Startup).Module.ModuleHandle);

        Localization.Culture = new CultureInfo("zh-Hans");
        Assert.Equal("必须指定节点", Localization.Translate("Node must be specified."));
        Assert.Equal("未登录！", Localization.Translate("Not logged in."));

        Localization.Culture = new CultureInfo("zh-Hant");
        Assert.Equal("必須指定節點", Localization.Translate("Node must be specified."));
    }

    [Fact(DisplayName = "启用多语言时应用核心默认语言并保留日志语言")]
    public void UseLocalizationAppliesDefaultLanguage()
    {
        var core = NewLife.Setting.Current;
        var oldLanguage = core.Language;
        var oldDefault = core.DefaultLanguage;
        var oldForced = Localization.ForcedLanguage;
        var oldLog = Localization.LogCulture;
        var oldDefaultCulture = Localization.DefaultCulture;
        try
        {
            core.Language = "";
            core.DefaultLanguage = "zh-CN";

            var app = new ApplicationBuilder(new ServiceCollection().BuildServiceProvider());
            global::Stardust.Server.StardustLocalizationExtensions.UseStardustLocalization(app);

            // DefaultLanguage 作为请求兜底，也是登录/心跳下发给客户端的平台默认语言
            Assert.Equal("zh-CN", Localization.DefaultCulture?.Name);
            // 日志语言跟随默认语言，不再被宿主语言覆盖
            Assert.Equal("zh-CN", Localization.LogCulture?.Name);
            // 界面语言仍按请求识别，不锁定
            Assert.Null(Localization.Culture);
        }
        finally
        {
            core.Language = oldLanguage;
            core.DefaultLanguage = oldDefault;
            Localization.SetLanguage(oldForced);
            Localization.LogCulture = oldLog;
            Localization.SetDefaultLanguage(oldDefaultCulture?.Name);
        }
    }
}

/// <summary>多语言测试集合。Localization.Culture 是进程级全局状态，禁止与其它测试并行</summary>
[CollectionDefinition("Localization", DisableParallelization = true)]
public class LocalizationCollection { }
