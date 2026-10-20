using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using NewLife;
using Xunit;

namespace Stardust.Tests;

/// <summary>星尘代理面板前端词表测试。验证 31 种语言键集一致，且 index.html 的中文源文全部可被词表覆盖</summary>
/// <remarks>
/// 面板词表（<c>StarAgent/WebPanel/wwwroot/i18n/*.json</c>）以中文源文为键，由 <c>i18n.js</c> 在浏览器端替换 DOM 文本节点、
/// placeholder 与 title。本测试复刻 <c>i18n.js</c> 的匹配算法：整串命中优先，否则按最长短语多轮替换，
/// 再用英文词表校验翻译后不再残留中文，避免新增文案忘记补词表。
/// </remarks>
public class StarPanelLocaleTests
{
    private const String _contentDir = "WebPanel";

    private static readonly Regex _styleBlock = new("<style[\\s\\S]*?</style>", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex _scriptBlock = new("<script(?![^>]*\\bsrc=)[^>]*>([\\s\\S]*?)</script>", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex _tag = new("<[^>]*>", RegexOptions.Compiled);
    private static readonly Regex _attr = new("\\b(placeholder|title|aria-label)\\s*=\\s*\"([^\"]*)\"", RegexOptions.Compiled);
    private static readonly Regex _jsLiteral = new("(['\"`])((?:\\\\.|(?!\\1)[^\\\\])*)\\1", RegexOptions.Compiled);
    private static readonly Regex _interpolation = new("\\$\\{[^}]*\\}", RegexOptions.Compiled);
    private static readonly Regex _placeholder = new("\\{(\\d+)(?:,-?\\d+)?(:[^}]*)?\\}", RegexOptions.Compiled);
    private static readonly Regex _space = new("\\s+", RegexOptions.Compiled);

    private static String GetContentPath(params String[] parts)
    {
        var path = Path.Combine(new[] { AppContext.BaseDirectory, _contentDir }.Concat(parts).ToArray());
        Assert.True(File.Exists(path) || Directory.Exists(path), $"未找到面板文件：{path}");

        return path;
    }

    private static Dictionary<String, String>[] ReadDictionaries()
    {
        var dir = GetContentPath("i18n");
        var files = Directory.GetFiles(dir, "*.json").OrderBy(e => e).ToArray();
        Assert.Equal(31, files.Length);

        return files.Select(file => JsonSerializer.Deserialize<Dictionary<String, String>>(File.ReadAllText(file, Encoding.UTF8))!).ToArray();
    }

    [Fact(DisplayName = "面板词表提供 31 种语言且键集一致")]
    public void PanelLocalesShareSameKeys()
    {
        var dicts = ReadDictionaries();
        var baseline = dicts[0].Keys.ToHashSet();

        for (var i = 1; i < dicts.Length; i++)
        {
            var keys = dicts[i].Keys.ToHashSet();
            var missing = baseline.Except(keys).ToArray();
            var extra = keys.Except(baseline).ToArray();

            Assert.True(missing.Length == 0, $"第 {i + 1} 个词表缺少 {missing.Length} 个键，如：{missing.FirstOrDefault()}");
            Assert.True(extra.Length == 0, $"第 {i + 1} 个词表多出 {extra.Length} 个键，如：{extra.FirstOrDefault()}");
        }

        foreach (var (key, value) in dicts.SelectMany(e => e))
        {
            Assert.False(value.IsNullOrEmpty(), $"译文为空：{key}");
            Assert.Equal(_placeholder.Matches(key).Count, _placeholder.Matches(value).Count);
        }
    }

    [Fact(DisplayName = "面板中文源文全部收录进词表")]
    public void PanelSourcesAreTranslatable()
    {
        var html = File.ReadAllText(GetContentPath("index.html"), Encoding.UTF8);
        // 英文词表不含中文，用它验证源文是否可被完整翻译
        var englishFile = Path.Combine(GetContentPath("i18n"), "en.json");
        var english = JsonSerializer.Deserialize<Dictionary<String, String>>(File.ReadAllText(englishFile, Encoding.UTF8))!;

        var sources = ExtractSources(html);
        Assert.NotEmpty(sources);

        var misses = new List<String>();
        foreach (var source in sources)
        {
            var text = Translate(english, source);
            if (text.Any(IsCjk)) misses.Add($"{source} => {text}");
        }

        Assert.True(misses.Count == 0, $"有 {misses.Count} 条中文源文未被词表覆盖：{Environment.NewLine}{String.Join(Environment.NewLine, misses)}");
    }

    /// <summary>从 index.html 抽取中文字面量：静态标记的文本节点与提示属性、内联脚本的字符串字面量</summary>
    private static HashSet<String> ExtractSources(String html)
    {
        var sources = new HashSet<String>();
        void Add(String? text)
        {
            if (text == null) return;

            var value = _space.Replace(text, " ").Trim();
            if (!value.IsNullOrEmpty() && value.Any(IsCjk)) sources.Add(value);
        }

        void ScanHtml(String source)
        {
            foreach (Match m in _attr.Matches(source)) Add(m.Groups[2].Value);

            var plain = _tag.Replace(source, "\n");
            foreach (var line in plain.Split('\n')) Add(line);
        }

        var scripts = _scriptBlock.Matches(html).Select(e => e.Groups[1].Value).ToArray();
        var markup = _scriptBlock.Replace(_styleBlock.Replace(html, ""), "");
        markup = Regex.Replace(markup, "<!--[\\s\\S]*?-->", "");

        ScanHtml(markup);
        foreach (var script in scripts)
        {
            foreach (Match m in _jsLiteral.Matches(script))
            {
                var raw = Unescape(m.Groups[2].Value);
                if (!raw.Any(IsCjk)) continue;

                foreach (var piece in _interpolation.Split(raw)) ScanHtml(piece);
            }
        }

        return sources;
    }

    /// <summary>复刻 i18n.js 的匹配算法：整串命中优先，否则按最长短语多轮替换</summary>
    private static String Translate(Dictionary<String, String> dict, String text)
    {
        var value = text.Trim();
        if (value.IsNullOrEmpty()) return text;

        if (dict.TryGetValue(value, out var exact)) return text.Replace(value, exact);

        if (value.Length < 2 || value.Length > 90) return text;

        var keys = dict.Keys.OrderByDescending(e => e.Length).ToArray();
        var rest = value;
        foreach (var key in keys)
        {
            if (!rest.Contains(key)) continue;

            var target = dict[key];
            if (target == key) continue;

            rest = key.Length < 2 ? ReplaceStandalone(rest, key, target) : rest.Replace(key, target);
        }

        return rest == value ? text : text.Replace(value, rest);
    }

    /// <summary>单字键只在两侧都不是汉字时替换，避免"核"命中"核对"之类的词</summary>
    private static String ReplaceStandalone(String text, String key, String value)
    {
        var sb = new StringBuilder();
        var index = 0;
        while (index <= text.Length)
        {
            var p = text.IndexOf(key, index, StringComparison.Ordinal);
            if (p < 0)
            {
                sb.Append(text, index, text.Length - index);
                break;
            }

            var before = p > 0 ? text[p - 1] : '\0';
            var after = p + key.Length < text.Length ? text[p + key.Length] : '\0';
            if (!IsCjk(before) && !IsCjk(after))
            {
                sb.Append(text, index, p - index);
                sb.Append(value);
            }
            else
            {
                sb.Append(text, index, p + key.Length - index);
            }

            index = p + key.Length;
        }

        return sb.ToString();
    }

    private static Boolean IsCjk(Char ch) => ch is >= '\u3400' and <= '\u9fff' or >= '\uf900' and <= '\ufaff';

    private static String Unescape(String text) => Regex.Replace(text, "\\\\(u[0-9a-fA-F]{4}|.)", m =>
    {
        var v = m.Groups[1].Value;
        if (v.Length == 5) return ((Char)Convert.ToInt32(v[1..], 16)).ToString();

        return v switch
        {
            "n" => "\n",
            "r" => "\r",
            "t" => "\t",
            "\\" => "\\",
            "\"" => "\"",
            "'" => "'",
            _ => m.Value,
        };
    });
}
