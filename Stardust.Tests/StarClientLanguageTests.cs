using System;
using System.Globalization;
using NewLife;
using Xunit;

namespace Stardust.Tests;

/// <summary>客户端默认语言测试。平台通过登录/心跳下发的默认语言不得覆盖本地固定语言</summary>
[Collection("Localization")]
public class StarClientLanguageTests
{
    private sealed class TestStarClient : StarClient
    {
        public TestStarClient(StarSetting setting) : base(setting) => Features = NewLife.Remoting.Clients.Features.Login;

        public void Apply(String language) => ApplyDefaultLanguage(language, false);
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

    [Fact(DisplayName = "本地固定语言后忽略平台下发的默认语言")]
    public void LocalLanguageWins()
    {
        using var state = new StateSnapshot();
        Localization.SetLanguage("en-US");
        Localization.SetDefaultLanguage(null);
        NewLife.Setting.Current.Language = "";
        NewLife.Setting.Current.DefaultLanguage = "";

        var set = new StarSetting { Server = "http://127.0.0.1:1" };
        using var client = new TestStarClient(set);

        client.Apply("zh-Hans");

        // 本地锁定 en-US 后，平台默认语言不能改动
        Assert.Equal("en-US", Localization.Culture?.Name);
        Assert.Equal("en-US", Localization.LogCulture?.Name);
        Assert.Null(Localization.DefaultCulture);
    }

    [Fact(DisplayName = "核心配置 Language 存在时忽略平台下发的默认语言")]
    public void CoreLanguageWins()
    {
        using var state = new StateSnapshot();
        Localization.SetLanguage(null);
        Localization.SetDefaultLanguage(null);
        NewLife.Setting.Current.Language = "en-US";
        NewLife.Setting.Current.DefaultLanguage = "";

        var set = new StarSetting { Server = "http://127.0.0.1:1" };
        using var client = new TestStarClient(set);

        client.Apply("zh-Hans");

        Assert.Null(Localization.DefaultCulture);
        Assert.Null(Localization.Culture);
    }

    [Fact(DisplayName = "未固定语言时应用平台下发的默认语言")]
    public void AppliesDefaultLanguageWithoutLocalLock()
    {
        using var state = new StateSnapshot();
        Localization.SetLanguage(null);
        Localization.SetDefaultLanguage(null);
        NewLife.Setting.Current.Language = "";
        NewLife.Setting.Current.DefaultLanguage = "";

        var set = new StarSetting { Server = "http://127.0.0.1:1" };
        using var client = new TestStarClient(set);

        client.Apply("en-US");

        Assert.Equal("en-US", Localization.DefaultCulture?.Name);
        Assert.Equal("en-US", Localization.LogCulture?.Name);
        // 默认语言只做兜底，不锁定界面
        Assert.Null(Localization.Culture);
    }

    [Fact(DisplayName = "StarClient /api 语言只影响进程内不写核心配置")]
    public void InMemoryOnlyDoesNotPersist()
    {
        using var state = new StateSnapshot();
        Localization.SetLanguage(null);
        Localization.SetDefaultLanguage(null);
        NewLife.Setting.Current.Language = "";
        NewLife.Setting.Current.DefaultLanguage = "";

        var set = new StarSetting { Server = "http://127.0.0.1:1" };
        using var client = new TestStarClient(set);

        client.Apply("en-US");

        Assert.Equal("en-US", Localization.DefaultCulture?.Name);
        Assert.Equal("", NewLife.Setting.Current.DefaultLanguage);
    }

    [Fact(DisplayName = "StarClient /api auto 只清进程内语言")]
    public void ResetsDefaultLanguageOnAuto()
    {
        using var state = new StateSnapshot();
        Localization.SetLanguage(null);
        Localization.SetDefaultLanguage("en-US");
        NewLife.Setting.Current.Language = "";
        NewLife.Setting.Current.DefaultLanguage = "en-US";

        var set = new StarSetting { Server = "http://127.0.0.1:1" };
        using var client = new TestStarClient(set);

        client.Apply(StarLocalization.AutoLanguage);

        Assert.Null(Localization.DefaultCulture);
        // /api 的 auto 只清进程内，不写核心配置
        Assert.Equal("en-US", NewLife.Setting.Current.DefaultLanguage);
        // 日志语言回到系统语言，不再跟随已清除的平台语言
        Assert.Equal(Localization.SystemCulture.Name, (Localization.LogCulture ?? Localization.SystemCulture).Name);
    }
}
