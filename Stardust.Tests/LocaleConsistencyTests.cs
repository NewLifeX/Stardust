using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NewLife;
using Xunit;

namespace Stardust.Tests;

/// <summary>词表一致性测试。验证各组件的 31 种语言词表键集一致、占位符一致，中文词表无遗漏</summary>
/// <remarks>词表文件由测试工程复制到输出目录 Locales 下，与编译期源生成器读取的是同一份源文件</remarks>
public class LocaleConsistencyTests
{
    private static readonly String[] _projects =
    [
        "Stardust", "Stardust.Extensions", "Stardust.Server", "Stardust.Web", "Stardust.WebApi", "Stardust.Data",
        "StarAgent", "StarAgent.Mobile", "StarGateway", "DeployAgent", "AgentExpansion", "NetworkDetect",
    ];

    private static readonly Regex _entry = new("^\\s*\"((?:[^\"\\\\]|\\\\.)*)\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"\\s*$", RegexOptions.Compiled);
    private static readonly Regex _placeholder = new("\\{(\\d+)(?:,-?\\d+)?(:[^}]*)?\\}", RegexOptions.Compiled);

    /// <summary>读取词表文件，返回英文源文到译文的映射</summary>
    private static Dictionary<String, String> ReadLocale(String path)
    {
        var dic = new Dictionary<String, String>();
        foreach (var line in File.ReadAllLines(path))
        {
            var m = _entry.Match(line);
            if (!m.Success) continue;

            var key = Unescape(m.Groups[1].Value);
            Assert.False(dic.ContainsKey(key), $"{Path.GetFileName(path)} 键重复：{key}");
            dic[key] = Unescape(m.Groups[2].Value);
        }

        return dic;
    }

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
            _ => m.Value,
        };
    });

    /// <summary>取占位符集合与格式说明符</summary>
    private static Dictionary<Int32, String> GetPlaceholders(String text)
    {
        var dic = new Dictionary<Int32, String>();
        foreach (Match m in _placeholder.Matches(text))
        {
            var index = Convert.ToInt32(m.Groups[1].Value);
            if (!dic.ContainsKey(index)) dic[index] = m.Groups[2].Value;
        }

        return dic;
    }

    private static String GetLocaleDir(String project)
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "Locales", project);
        Assert.True(Directory.Exists(dir), $"未找到词表目录：{dir}");

        return dir;
    }

    [Fact(DisplayName = "各组件均提供 31 种语言词表")]
    public void AllProjectsHaveAllLocales()
    {
        foreach (var project in _projects)
        {
            var dir = GetLocaleDir(project);
            var files = Directory.GetFiles(dir, "*.yml");

            Assert.Equal(31, files.Length);
        }
    }

    [Fact(DisplayName = "同一组件的各语言词表键集与占位符完全一致")]
    public void LocaleKeysAndPlaceholdersMatch()
    {
        foreach (var project in _projects)
        {
            var dir = GetLocaleDir(project);
            var baseline = ReadLocale(Path.Combine(dir, "zh-Hans.yml"));

            foreach (var file in Directory.GetFiles(dir, "*.yml"))
            {
                var name = Path.GetFileName(file);
                if (name == "zh-Hans.yml") continue;

                var locale = ReadLocale(file);

                foreach (var key in baseline.Keys)
                {
                    Assert.True(locale.ContainsKey(key), $"{project}/{name} 缺少键：{key}");
                }
                foreach (var key in locale.Keys)
                {
                    Assert.True(baseline.ContainsKey(key), $"{project}/{name} 多余键：{key}");
                }

                foreach (var (key, value) in locale)
                {
                    var expected = GetPlaceholders(key);
                    var actual = GetPlaceholders(value);
                    Assert.True(expected.Count == actual.Count && !expected.Keys.Except(actual.Keys).Any(),
                        $"{project}/{name} 占位符不一致：{key} => {value}");

                    foreach (var (index, spec) in expected)
                    {
                        if (spec.IsNullOrEmpty()) continue;
                        Assert.True(actual.TryGetValue(index, out var actualSpec) && actualSpec == spec,
                            $"{project}/{name} 格式说明符不一致：{key} => {value}");
                    }

                    Assert.False(value.IsNullOrEmpty(), $"{project}/{name} 译文为空：{key}");
                }
            }
        }
    }

    [Fact(DisplayName = "简体与繁体词表全部提供译文")]
    public void ChineseLocalesFullyTranslated()
    {
        foreach (var project in _projects)
        {
            var dir = GetLocaleDir(project);

            foreach (var name in new[] { "zh-Hans.yml", "zh-Hant.yml" })
            {
                var locale = ReadLocale(Path.Combine(dir, name));
                var missing = locale.Where(e => e.Key == e.Value).Select(e => e.Key).ToArray();

                Assert.True(missing.Length == 0, $"{project}/{name} 未翻译 {missing.Length} 条，如：{missing.FirstOrDefault()}");
            }
        }
    }
}
