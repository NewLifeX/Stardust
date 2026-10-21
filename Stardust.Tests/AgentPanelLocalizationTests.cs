using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
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

        // 未固定语言时跟随服务端实际生效语言，提示属性同样参与翻译
        Assert.Contains("effective", js);
        Assert.Contains("aria-label", js);
    }

    [Fact(DisplayName = "面板服务端本地化不改写内联脚本")]
    public void PanelServerLocalizationKeepsScripts()
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "WebPanel");
        var html = File.ReadAllText(Path.Combine(dir, "index.html"));
        var scripts = ExtractScripts(html);
        Assert.NotEmpty(scripts);

        // 译文里的撇号若写进内联脚本的单引号字符串，会造成语法错误（法语、乌克兰语已复现）
        foreach (var file in Directory.GetFiles(Path.Combine(dir, "i18n"), "*.json"))
        {
            var language = Path.GetFileNameWithoutExtension(file);
            var map = JsonSerializer.Deserialize<Dictionary<String, String>>(File.ReadAllText(file))!;
            var localized = StarAgent.WebPanel.StarPanel.LocalizeHtml(html, language, map);

            Assert.Contains($"<html lang=\"{language}\"", localized);
            foreach (var script in scripts) Assert.Contains(script, localized);
        }
    }

    [Fact(DisplayName = "面板服务端本地化翻译静态文案")]
    public void PanelServerLocalizationTranslatesMarkup()
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "WebPanel");
        var html = File.ReadAllText(Path.Combine(dir, "index.html"));
        var map = JsonSerializer.Deserialize<Dictionary<String, String>>(File.ReadAllText(Path.Combine(dir, "i18n", "fr.json")))!;

        var localized = StarAgent.WebPanel.StarPanel.LocalizeHtml(html, "fr", map);

        Assert.Contains("Saisissez vos identifiants pour continuer", localized);
        Assert.DoesNotContain("请输入凭据以继续", localized);
    }

    /// <summary>抽取页面内联脚本内容</summary>
    private static String[] ExtractScripts(String html)
    {
        var list = new List<String>();
        var index = 0;
        while (index < html.Length)
        {
            var start = html.IndexOf("<script", index, StringComparison.OrdinalIgnoreCase);
            if (start < 0) break;

            var open = html.IndexOf('>', start);
            if (open < 0) break;

            var end = html.IndexOf("</script>", open, StringComparison.OrdinalIgnoreCase);
            if (end < 0) break;

            list.Add(html[(open + 1)..end]);
            index = end + "</script>".Length;
        }

        return list.ToArray();
    }
}
