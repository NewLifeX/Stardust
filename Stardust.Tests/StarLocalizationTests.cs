using System;
using System.Globalization;
using NewLife;
using Xunit;

namespace Stardust.Tests;

/// <summary>语言名称归一化测试。验证区域语言、旧语言码与中文文字版本的映射</summary>
[Collection("Localization")]
public class StarLocalizationTests : IDisposable
{
    private readonly CultureInfo? _old = Localization.Culture;
    private readonly CultureInfo? _oldLog = Localization.LogCulture;

    /// <summary>恢复原始语言</summary>
    public void Dispose()
    {
        Localization.Culture = _old;
        Localization.LogCulture = _oldLog;
    }

    [Theory(DisplayName = "区域语言与裸语言码归一化到词表语言")]
    [InlineData("zh-CN", "zh-Hans")]
    [InlineData("zh-SG", "zh-Hans")]
    [InlineData("zh", "zh-Hans")]
    [InlineData("zh-TW", "zh-Hant")]
    [InlineData("zh-HK", "zh-Hant")]
    [InlineData("zh-Hant", "zh-Hant")]
    [InlineData("en", "en")]
    [InlineData("en-US", "en")]
    [InlineData("ja", "ja")]
    [InlineData("ja-JP", "ja")]
    [InlineData("de-AT", "de")]
    [InlineData("pt-PT", "pt")]
    [InlineData("nb-NO", "nb")]
    [InlineData("no", "nb")]
    [InlineData("iw", "he")]
    [InlineData("in", "id")]
    public void NormalizeCulture(String name, String expected) => Assert.Equal(expected, StarLocalization.NormalizeCulture(name));

    [Theory(DisplayName = "带权重的请求头取首选语言")]
    [InlineData("fr-CA,fr;q=0.9,en;q=0.8", "fr")]
    [InlineData("de-AT;q=0.7,en;q=0.9", "de")]
    public void NormalizeWeightedHeader(String name, String expected) => Assert.Equal(expected, StarLocalization.NormalizeCulture(name));

    [Theory(DisplayName = "不支持与空语言返回空")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("sw-KE")]
    [InlineData("xx")]
    public void NormalizeUnsupported(String? name) => Assert.Null(StarLocalization.NormalizeCulture(name));

    [Fact(DisplayName = "词表语言全部可归一化且包含中英文")]
    public void SupportedCulturesAreNormalizable()
    {
        Assert.Contains("zh-Hans", StarLocalization.SupportedCultures);
        Assert.Contains("zh-Hant", StarLocalization.SupportedCultures);
        Assert.Contains("en", StarLocalization.SupportedCultures);

        foreach (var item in StarLocalization.SupportedCultures)
        {
            Assert.Equal(item, StarLocalization.NormalizeCulture(item));
        }
    }

    [Fact(DisplayName = "按语言设置进程界面与日志语言")]
    public void UseLanguageSetsProcessCulture()
    {
        Localization.Culture = null;
        Localization.LogCulture = null;

        Assert.Equal("ja", StarLocalization.UseLanguage("ja-JP"));
        Assert.Equal("ja", Localization.Culture?.Name);
        Assert.Equal("ja", Localization.LogCulture?.Name);

        // 不支持的语言不改动现有设置
        Assert.Null(StarLocalization.UseLanguage("sw-KE"));
        Assert.Equal("ja", Localization.Culture?.Name);
    }
}
