using System;
using System.Globalization;
using System.Runtime.CompilerServices;
using NewLife;
using Xunit;

namespace Stardust.Tests;

/// <summary>多语言词表测试。验证 31 种语言全部注册并可翻译，以及未提供语言时回退英文</summary>
[Collection("Localization")]
public class LocalizationTests : IDisposable
{
    private static readonly String[] _locales =
    [
        "ar", "bn", "cs", "da", "de", "el", "es", "fa", "fi", "fr", "he", "hi", "hu", "id", "it", "ja", "ko",
        "ms", "nb", "nl", "pl", "pt", "ro", "ru", "sv", "th", "tr", "uk", "vi", "zh-Hans", "zh-Hant",
    ];

    private readonly CultureInfo? _old = Localization.Culture;
    private readonly CultureInfo? _oldLog = Localization.LogCulture;

    /// <summary>恢复原始目标语言</summary>
    public void Dispose()
    {
        Localization.Culture = _old;
        Localization.LogCulture = _oldLog;
    }

    /// <summary>执行各组件生成的模块初始化，注册词表</summary>
    private static void LoadAssemblies()
    {
        RuntimeHelpers.RunModuleConstructor(typeof(global::Stardust.StarClient).Module.ModuleHandle);
        RuntimeHelpers.RunModuleConstructor(typeof(global::StarAgent.StarService).Module.ModuleHandle);
        RuntimeHelpers.RunModuleConstructor(typeof(global::StarAgent.Mobile.MobileAgent).Module.ModuleHandle);
        RuntimeHelpers.RunModuleConstructor(typeof(global::Microsoft.Extensions.DependencyInjection.StarFactoryExtensions).Module.ModuleHandle);
    }

    [Fact(DisplayName = "星尘核心库与星尘代理的31种语言词表全部注册")]
    public void AllLocalesRegistered()
    {
        LoadAssemblies();

        Assert.Equal(31, _locales.Length);

        foreach (var code in _locales)
        {
            Localization.Culture = new CultureInfo(code);

            Assert.NotEqual("No version was specified.", Localization.Translate("No version was specified."));
            Assert.NotEqual("The configuration file does not exist.", Localization.Translate("The configuration file does not exist."));
        }
    }

    [Fact(DisplayName = "未提供词表的语言回退英文原文")]
    public void UnknownLocaleFallbackToEnglish()
    {
        LoadAssemblies();

        Localization.Culture = new CultureInfo("sw-KE");

        Assert.Equal("No version was specified.", Localization.Translate("No version was specified."));
    }

    [Fact(DisplayName = "日志语言与界面语言相互独立")]
    public void LogCultureIsIndependent()
    {
        LoadAssemblies();

        Localization.Culture = new CultureInfo("zh-Hans");
        Localization.LogCulture = new CultureInfo("ja");

        Assert.Equal("未指定版本！", L.T("No version was specified."));
        Assert.Equal("バージョンが指定されていません。", L.TLog("No version was specified."));
    }

    [Fact(DisplayName = "扩展库词表注册并支持中文翻译")]
    public void ExtensionsLocalesRegistered()
    {
        LoadAssemblies();

        Localization.Culture = new CultureInfo("zh-Hans");
        Assert.Equal("未注册StarFactory，请调用AddStardust注册。", Localization.Translate("StarFactory is not registered; call AddStardust to register."));

        Localization.Culture = new CultureInfo("zh-Hant");
        Assert.Equal("未註冊StarFactory，請調用AddStardust註冊。", Localization.Translate("StarFactory is not registered; call AddStardust to register."));
    }

    [Fact(DisplayName = "代理与移动端词表注册并支持中文翻译")]
    public void AgentLocalesRegistered()
    {
        LoadAssemblies();

        Localization.Culture = new CultureInfo("zh-Hans");
        Assert.Equal("启动成功！PID=123", Localization.Translate("Started successfully, PID={0}", 123));
        Assert.Equal("[Android] 登录成功，节点编码：abc", Localization.Translate("[{0}] Login succeeded, node code: {1}", "Android", "abc"));

        Localization.Culture = new CultureInfo("zh-Hant");
        Assert.Equal("啟動成功！PID=123", Localization.Translate("Started successfully, PID={0}", 123));
    }

    [Fact(DisplayName = "星尘设置描述支持中文与繁体翻译")]
    public void SettingDescriptions()
    {
        LoadAssemblies();

        Localization.Culture = new CultureInfo("zh-Hans");
        Assert.Equal("调试开关。默认false", Localization.Translate("Debug switch. Default false"));
        Assert.Equal("星尘设置", Localization.Translate("Stardust settings"));

        Localization.Culture = new CultureInfo("zh-Hant");
        Assert.Equal("偵錯開關。預設 false", Localization.Translate("Debug switch. Default false"));
        Assert.Equal("星塵設定", Localization.Translate("Stardust settings"));
    }
}

/// <summary>多语言测试集合。Localization.Culture 是进程级全局状态，禁止与其它测试并行</summary>
[CollectionDefinition("Localization", DisableParallelization = true)]
public class LocalizationCollection { }
