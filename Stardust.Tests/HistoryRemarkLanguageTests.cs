using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using NewLife;
using Xunit;

namespace Stardust.Tests;

/// <summary>历史备注语言约定测试。历史备注是事件快照，跟随服务器日志语言（L.TLog），不随触发者的界面语言变化</summary>
public class HistoryRemarkLanguageTests
{
    /// <summary>从测试输出目录向上定位仓库根目录</summary>
    private static String? FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "星尘.slnx"))) return dir.FullName;
            dir = dir.Parent;
        }

        return null;
    }

    [Fact(DisplayName = "历史备注使用日志语言 L.TLog")]
    public void HistoryRemarksUseLogLanguage()
    {
        var root = FindRepoRoot();
        if (root == null) return;

        var bad = new List<String>();
        foreach (var name in new[] { "Stardust.Server", "Stardust.Data", "Stardust.Web", "Stardust.WebApi" })
        {
            var dir = Path.Combine(root, name);
            if (!Directory.Exists(dir)) continue;

            foreach (var file in Directory.EnumerateFiles(dir, "*.cs", SearchOption.AllDirectories))
            {
                if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") ||
                    file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")) continue;

                var lines = File.ReadAllLines(file);
                var writesHistory = false;
                foreach (var item in lines)
                {
                    if (item.Contains("WriteHistory(") || item.Contains("NodeHistory.Create(") || item.Contains("AppHistory.Create(")) writesHistory = true;
                }
                if (!writesHistory) continue;

                for (var i = 0; i < lines.Length; i++)
                {
                    var line = lines[i].Trim();
                    if (line.StartsWith("//")) continue;

                    // 变量转交写法：var msg = L.T(...) 之后再传给 WriteHistory，同样不允许
                    if (line.Contains("var msg = L.T(") || line.Contains("var remark = L.T("))
                    {
                        bad.Add($"{name}/{Path.GetFileName(file)}:{i + 1}");
                        continue;
                    }

                    if (!line.Contains("WriteHistory(") && !line.Contains("NodeHistory.Create(") && !line.Contains("AppHistory.Create(")) continue;
                    if (line.Contains("L.T(")) bad.Add($"{name}/{Path.GetFileName(file)}:{i + 1}");
                }
            }
        }

        Assert.True(bad.Count == 0, $"历史备注必须用 L.TLog 跟随日志语言：{String.Join(", ", bad)}");
    }

    [Fact(DisplayName = "日志语言独立于界面语言")]
    public void LogLanguageIsIndependent()
    {
        var oldCulture = Localization.Culture;
        var oldLog = Localization.LogCulture;
        try
        {
            // 界面英文、日志中文：历史备注取日志语言
            Localization.Culture = CultureInfo.GetCultureInfo("en");
            Localization.LogCulture = CultureInfo.GetCultureInfo("zh-Hans");

            Assert.Equal("应用鉴权", Localization.TranslateForLog("Application authentication"));
        }
        finally
        {
            Localization.Culture = oldCulture;
            Localization.LogCulture = oldLog;
        }
    }
}
