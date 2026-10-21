#if !NET40
using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using NewLife;
using NewLife.Agent;
using NewLife.Agent.WebPanel;
using NewLife.Data;
using NewLife.Http;

namespace StarAgent.WebPanel;

/// <summary>StarAgent Web管理面板</summary>
/// <remarks>
/// 继承 AgentWebPanel，定制 StarAgent 专有功能：
/// - 重写嵌入资源路径以使用自己的静态文件
/// - 注册自定义 API 控制器（子服务管理、星尘配置、本机信息）
/// - 注册扩展面板
/// </remarks>
/// <remarks>实例化StarAgent Web管理面板</remarks>
/// <param name="service">所属服务</param>
public class StarPanel(ServiceBase service) : AgentWebPanel(service)
{
    #region 属性
    /// <summary>嵌入式静态文件的资源命名空间前缀</summary>
    protected override String EmbeddedResourcePrefix => "StarAgent.WebPanel.wwwroot";

    /// <summary>嵌入式静态文件所在程序集的标记类型</summary>
    protected override Type EmbeddedResourceAssemblyType => typeof(StarPanel);

    /// <summary>所属服务</summary>
    public new ServiceBase Service { get; } = service;
    #endregion

    #region 路由注册
    /// <summary>注册所有路由</summary>
    /// <remarks>
    /// 先注册 StarApi 路由，再调用基类注册内置 API 和静态文件。
    /// 基类注册的 /* 通配符会放在 Dictionary 末尾，因此通配匹配时
    /// /star/* 会优先于 /* 被检查到，避免被静态文件处理器拦截。
    /// </remarks>
    protected override void RegisterRoutes()
    {
        // 1. 先注册 StarApi 控制器（通配 /star/*），确保排在 /* 前面
        Server.MapController<StarApi>("/star");

        // 2. 再调用基类注册内置 API（/api/*）+ 静态文件（/*）
        base.RegisterRoutes();
    }
    #endregion

    #region 首页本地化
    /// <summary>本地化时保持原样的区块：脚本与样式</summary>
    private static readonly Regex _protectedBlocks = new("(<script\\b[^>]*>[\\s\\S]*?</script>|<style\\b[^>]*>[\\s\\S]*?</style>)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>本地化首页缓存。Key 为语言</summary>
    private static readonly ConcurrentDictionary<String, String> _pages = new(StringComparer.Ordinal);

    /// <summary>输出按当前语言本地化的首页</summary>
    /// <remarks>
    /// 基类对整页做原文替换，译文中的撇号会写进内联脚本的单引号字符串，导致脚本语法错误（法语、乌克兰语已复现）。
    /// 这里只替换脚本与样式之外的静态标记，脚本内的中文由浏览器端 i18n.js 按同一份词表翻译。
    /// </remarks>
    /// <param name="ctx">Http上下文</param>
    protected override void WriteIndex(IHttpContext ctx)
    {
        var culture = Localization.Culture ?? CultureInfo.CurrentUICulture;
        var language = UiLocalization.GetLanguage(culture);
        var html = _pages.GetOrAdd(language, lang => LocalizeHtml(ReadIndexHtml(), lang,
            UiLocalization.GetDictionary(EmbeddedResourceAssemblyType.Assembly, EmbeddedResourcePrefix, lang)));

        ctx.Response.ContentType = "text/html; charset=utf-8";
        ctx.Response.Headers["Cache-Control"] = "no-cache";
        ctx.Response.Body = new ArrayPacket(html.GetBytes());
    }

    /// <summary>本地化首页源文</summary>
    /// <param name="html">中文源文</param>
    /// <param name="language">目标语言</param>
    /// <param name="map">中文到目标语言的词表</param>
    /// <returns>本地化后的页面</returns>
    public static String LocalizeHtml(String html, String language, IDictionary<String, String> map)
    {
        if (html.IsNullOrEmpty()) return html;

        if (language != "zh-Hans" && map != null && map.Count > 0)
        {
            var parts = _protectedBlocks.Split(html);
            var keys = map.OrderByDescending(e => e.Key.Length).ToArray();
            for (var i = 0; i < parts.Length; i += 2)
            {
                foreach (var item in keys)
                {
                    if (item.Key.IsNullOrEmpty() || item.Value.IsNullOrEmpty()) continue;

                    parts[i] = parts[i].Replace(item.Key, item.Value);
                }
            }
            html = String.Concat(parts);
        }

        var dir = UiLocalization.IsRightToLeft(language) ? " dir=\"rtl\"" : "";
        return Regex.Replace(html, "<html[^>]*>", $"<html lang=\"{language}\"{dir}>", RegexOptions.IgnoreCase);
    }

    /// <summary>读取嵌入的首页源文</summary>
    /// <returns>首页 HTML；资源不存在时返回空串</returns>
    private String ReadIndexHtml()
    {
        using var stream = EmbeddedResourceAssemblyType.Assembly.GetManifestResourceStream($"{EmbeddedResourcePrefix}.index.html");
        if (stream == null) return "";

        using var reader = new StreamReader(stream, Encoding.UTF8);

        return reader.ReadToEnd();
    }
    #endregion
}
#endif
