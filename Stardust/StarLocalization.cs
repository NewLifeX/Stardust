using System.Globalization;
using NewLife;

namespace Stardust;

/// <summary>星尘多语言辅助。把配置或请求中的语言名称归一化到可用词表</summary>
/// <remarks>
/// 词表来自各组件编译期 <c>Locales/*.yml</c>，英文为源文与兜底。
/// 归一化结果交给 <see cref="Localization"/>：<c>zh-CN</c> 命中 <c>zh-Hans</c>，<c>zh-TW</c> 命中 <c>zh-Hant</c>，<c>de-AT</c> 命中 <c>de</c>。
/// </remarks>
public static class StarLocalization
{
    #region 属性
    /// <summary>可用词表语言。英文是源文与兜底，不需要单独的 en 词表文件</summary>
    public static String[] SupportedCultures { get; } =
    [
        "zh-Hans", "zh-Hant", "en",
        "ar", "bn", "cs", "da", "de", "el", "es", "fa", "fi", "fr", "he", "hi", "hu", "id", "it", "ja", "ko",
        "ms", "nb", "nl", "pl", "pt", "ro", "ru", "sv", "th", "tr", "uk", "vi",
    ];
    #endregion

    #region 方法
    /// <summary>归一化语言名称。支持裸语言码、区域语言与带权重的请求头片段</summary>
    /// <param name="name">语言名称，如 zh-CN、zh-TW、de-AT、ja，或 en-US,en;q=0.9</param>
    /// <returns>受支持的语言名称；无法匹配时返回 null</returns>
    public static String? NormalizeCulture(String? name)
    {
        if (name.IsNullOrWhiteSpace()) return null;

        // 取首个候选并去掉权重
        var first = name!.Split(',')[0].Trim();
        var p = first.IndexOf(';');
        if (p > 0) first = first[..p].Trim();
        if (first.IsNullOrEmpty()) return null;

        // 兼容旧语言码
        first = first.ToLowerInvariant() switch
        {
            "iw" => "he",
            "in" => "id",
            "no" => "nb",
            _ => first,
        };

        foreach (var item in SupportedCultures)
        {
            if (item.EqualIgnoreCase(first)) return item;
        }

        // 中文按文字版本区分，其余区域语言退化到语言前缀
        if (first.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
        {
            var lower = first.ToLowerInvariant();
            return lower.Contains("hant") || lower == "zh-tw" || lower == "zh-hk" || lower == "zh-mo" ? "zh-Hant" : "zh-Hans";
        }

        var dash = first.IndexOf('-');
        var lang = dash > 0 ? first[..dash] : first;
        foreach (var item in SupportedCultures)
        {
            if (item.EqualIgnoreCase(lang)) return item;
        }

        return null;
    }

    /// <summary>按语言名称固定进程界面与日志语言</summary>
    /// <param name="language">语言名称，为空时不改动</param>
    /// <returns>实际生效的语言名称；未设置或无法识别时返回 null</returns>
    public static String? UseLanguage(String? language)
    {
        var name = NormalizeCulture(language);
        if (name == null) return null;

        try
        {
            var culture = new CultureInfo(name);
            Localization.Culture = culture;
            Localization.LogCulture = culture;
            return name;
        }
        catch (CultureNotFoundException)
        {
            // 无 ICU 的 invariant 模式不允许创建具体区域性，保持英文源文
            return null;
        }
    }
    #endregion
}
