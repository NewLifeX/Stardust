using System;
using System.Globalization;
using System.IO;
using NewLife;
using Xunit;

namespace Stardust.Tests;

/// <summary>代理面板服务端界面文案多语言测试</summary>
/// <remarks>
/// <c>StarApi</c> 的配置项分组、字段名与说明走 <c>L.T</c>，运行时长走 <c>L.T("{0}d {1}h {2}m")</c> 等模板。
/// 这里用德语验证词表已随程序集加载，避免只补齐键而运行期取不到译文。
/// </remarks>
public class AgentPanelLocalizationTests
{
    [Fact(DisplayName = "代理面板配置文案按语言返回译文")]
    public void PanelConfigTextIsLocalized()
    {
        // 词表随程序集加载时注册，先触达 StarAgent 程序集
        Assert.NotNull(typeof(StarAgent.WebPanel.StarApi).Assembly);

        var culture = Localization.Culture;
        try
        {
            Localization.Culture = new CultureInfo("de");

            Assert.Equal("Serveradresse", L.T("Server address"));
            Assert.Equal("Lokaler API-Port (UDP), Standard 5500. Teilt den Port mit dem Webpanel: UDP für lokales RPC, TCP für das Webpanel",
                L.T("Local API port (UDP), default 5500. Shares the port with the web panel: UDP for local RPC, TCP for the web panel"));
        }
        finally
        {
            Localization.Culture = culture;
        }
    }

    [Fact(DisplayName = "运行时长模板按语言格式化")]
    public void UptimeTemplateIsLocalized()
    {
        var culture = Localization.Culture;
        try
        {
            Localization.Culture = new CultureInfo("zh-Hans");
            Assert.Equal("2天3小时4分", L.T("{0}d {1}h {2}m", 2, 3, 4));

            Localization.Culture = new CultureInfo("ja");
            Assert.Equal("2日3時間4分", L.T("{0}d {1}h {2}m", 2, 3, 4));
        }
        finally
        {
            Localization.Culture = culture;
        }
    }

    [Fact(DisplayName = "面板鉴权与服务管理器缺失消息按语言返回")]
    public void PanelMessagesAreLocalized()
    {
        var culture = Localization.Culture;
        try
        {
            Localization.Culture = new CultureInfo("zh-Hans");
            Assert.Equal("未授权", L.T("Unauthorized"));
            Assert.Equal("服务管理器不可用", L.T("Service manager not available"));

            Localization.Culture = new CultureInfo("de");
            Assert.Equal("Nicht autorisiert", L.T("Unauthorized"));
            Assert.Equal("Dienstmanager nicht verfügbar", L.T("Service manager not available"));
        }
        finally
        {
            Localization.Culture = culture;
        }
    }

    [Fact(DisplayName = "面板语言选择写入服务端配置")]
    public void PanelLanguageSwitchUsesServerApi()
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "WebPanel");
        var html = File.ReadAllText(Path.Combine(dir, "index.html"));
        var js = File.ReadAllText(Path.Combine(dir, "i18n.js"));

        // 语言写入 Core 全局配置，接口请求不再携带 lang 参数
        Assert.Contains("/api/setLanguage?language=", html);
        Assert.Contains("/api/languages", js);
        Assert.DoesNotContain("staragent.lang", js);
        Assert.DoesNotContain("'lang='+StarI18N.lang", html);
    }
}
