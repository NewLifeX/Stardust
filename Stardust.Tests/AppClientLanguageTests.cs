using System;
using System.Globalization;
using NewLife;
using Xunit;

namespace Stardust.Tests;

/// <summary>应用客户端默认语言测试。客户端选项、平台下发语言与本地固定语言的处理</summary>
[Collection("Localization")]
public class AppClientLanguageTests
{
    private sealed class TestAppClient : AppClient
    {
        public TestAppClient(StarSetting setting) => Setting = setting;

        public void Apply(String language, Boolean save = false) => ApplyDefaultLanguage(language, save);
    }

    /// <summary>保存并恢复进程级语言状态，避免污染其它测试</summary>
    private sealed class StateSnapshot : IDisposable
    {
        private readonly CultureInfo? _culture = Localization.Culture;
        private readonly CultureInfo? _logCulture = Localization.LogCulture;
        private readonly String? _forced = Localization.ForcedLanguage;
        private readonly CultureInfo? _default = Localization.DefaultCulture;
        private readonly String? _coreLanguage = NewLife.Setting.Current.Language;
        private readonly String? _coreDefault = NewLife.Setting.Current.DefaultLanguage;

        public void Dispose()
        {
            NewLife.Setting.Current.Language = _coreLanguage ?? "";
            NewLife.Setting.Current.DefaultLanguage = _coreDefault ?? "";
            Localization.SetLanguage(_forced);
            Localization.Culture = _culture;
            Localization.LogCulture = _logCulture;
            Localization.SetDefaultLanguage(_default?.Name);
        }
    }

    [Fact(DisplayName = "应用客户端本地固定语言后忽略平台下发的默认语言")]
    public void LocalLanguageWins()
    {
        using var state = new StateSnapshot();
        Localization.SetLanguage("en-US");
        Localization.SetDefaultLanguage(null);
        NewLife.Setting.Current.Language = "";
        NewLife.Setting.Current.DefaultLanguage = "";

        using var client = new TestAppClient(new StarSetting { Server = "http://127.0.0.1:1" });

        client.Apply("zh-Hans");

        Assert.Equal("en-US", Localization.Culture?.Name);
        Assert.Equal("en-US", Localization.LogCulture?.Name);
        Assert.Null(Localization.DefaultCulture);
    }

    [Fact(DisplayName = "应用客户端核心配置 Language 存在时忽略平台下发的默认语言")]
    public void CoreLanguageWins()
    {
        using var state = new StateSnapshot();
        Localization.SetLanguage(null);
        Localization.SetDefaultLanguage(null);
        NewLife.Setting.Current.Language = "en-US";
        NewLife.Setting.Current.DefaultLanguage = "";

        using var client = new TestAppClient(new StarSetting { Server = "http://127.0.0.1:1" });

        client.Apply("zh-Hans");

        Assert.Null(Localization.DefaultCulture);
        Assert.Null(Localization.Culture);
    }

    [Fact(DisplayName = "应用客户端关闭接受平台语言后忽略平台下发的默认语言")]
    public void AcceptPlatformLanguageFalse()
    {
        using var state = new StateSnapshot();
        Localization.SetLanguage(null);
        Localization.SetDefaultLanguage(null);
        NewLife.Setting.Current.Language = "";
        NewLife.Setting.Current.DefaultLanguage = "";

        using var client = new TestAppClient(new StarSetting { Server = "http://127.0.0.1:1" }) { AcceptPlatformLanguage = false };

        client.Apply("en-US");

        Assert.Null(Localization.DefaultCulture);
        Assert.Null(Localization.Culture);
        Assert.Equal("", NewLife.Setting.Current.DefaultLanguage);
    }

    [Fact(DisplayName = "应用客户端 /api 语言只影响进程内不写核心配置")]
    public void InMemoryOnlyDoesNotPersist()
    {
        using var state = new StateSnapshot();
        Localization.SetLanguage(null);
        Localization.SetDefaultLanguage(null);
        NewLife.Setting.Current.Language = "";
        NewLife.Setting.Current.DefaultLanguage = "";

        using var client = new TestAppClient(new StarSetting { Server = "http://127.0.0.1:1" });

        client.Apply("en-US", false);

        Assert.Equal("en-US", Localization.DefaultCulture?.Name);
        Assert.Equal("", NewLife.Setting.Current.DefaultLanguage);
    }

    [Fact(DisplayName = "应用客户端未固定语言时应用平台下发的默认语言")]
    public void AppliesDefaultLanguageWithoutLocalLock()
    {
        using var state = new StateSnapshot();
        Localization.SetLanguage(null);
        Localization.SetDefaultLanguage(null);
        NewLife.Setting.Current.Language = "";
        NewLife.Setting.Current.DefaultLanguage = "";

        using var client = new TestAppClient(new StarSetting { Server = "http://127.0.0.1:1" });

        client.Apply("en-US");

        Assert.Equal("en-US", Localization.DefaultCulture?.Name);
        Assert.Equal("en-US", Localization.LogCulture?.Name);
        Assert.Null(Localization.Culture);
    }

    [Fact(DisplayName = "应用客户端收到 auto 时清除本机默认语言")]
    public void ResetsDefaultLanguageOnAuto()
    {
        using var state = new StateSnapshot();
        Localization.SetLanguage(null);
        Localization.SetDefaultLanguage("en-US");
        NewLife.Setting.Current.Language = "";
        NewLife.Setting.Current.DefaultLanguage = "en-US";

        using var client = new TestAppClient(new StarSetting { Server = "http://127.0.0.1:1" });

        client.Apply(StarLocalization.AutoLanguage, false);

        Assert.Null(Localization.DefaultCulture);
        // /api 的 auto 只清进程内，不写核心配置
        Assert.Equal("en-US", NewLife.Setting.Current.DefaultLanguage);
    }
}
