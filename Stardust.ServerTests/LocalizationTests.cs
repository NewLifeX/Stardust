using System;
using System.Globalization;
using System.Runtime.CompilerServices;
using NewLife;
using Xunit;

namespace ServerTest;

/// <summary>数据层多语言词表测试。验证 31 种语言全部注册并可翻译</summary>
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
}
