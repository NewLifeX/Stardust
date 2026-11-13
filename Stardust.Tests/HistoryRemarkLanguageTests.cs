using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using NewLife;
using Xunit;

namespace Stardust.Tests;

/// <summary>历史备注语言约定测试。历史备注是事件快照，跟随服务器日志语言（L.TLog），不随触发者的界面语言变化</summary>
/// <remarks>Localization.Culture/LogCulture 是进程级全局状态，与其它多语言测试同集合串行，避免并行互踩（run1 红/重跑绿 flake 根因）</remarks>
[Collection("Localization")]
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
        foreach (var name in new[] { "Stardust", "Stardust.Server", "Stardust.Data", "Stardust.Web", "Stardust.WebApi", "StarAgent" })
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
                    if (item.Contains("WriteHistory(") || item.Contains("NodeHistory.Create(") || item.Contains("AppHistory.Create(") ||
                        item.Contains("WriteInfoEvent(") || item.Contains("WriteErrorEvent(") || item.Contains("WriteEvent(")) writesHistory = true;
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

                    if (!line.Contains("WriteHistory(") && !line.Contains("NodeHistory.Create(") && !line.Contains("AppHistory.Create(") &&
                        !line.Contains("WriteInfoEvent(") && !line.Contains("WriteErrorEvent(") && !line.Contains("WriteEvent(")) continue;
                    if (line.Contains("L.T(")) bad.Add($"{name}/{Path.GetFileName(file)}:{i + 1}");
                }
            }
        }

        Assert.True(bad.Count == 0, $"历史备注必须用 L.TLog 跟随日志语言：{String.Join(", ", bad)}");
    }

    [Fact(DisplayName = "日志语言独立于界面语言")]
    public void LogLanguageIsIndependent()
    {
        // 自预热：词表注册是进程级懒加载，本测试不再依赖同集合其它测试先跑（跨进程测试类发现序抖动会导致本测试先执行时词表未注册，TranslateForLog 原文回退——run1 绿/重跑红 flake 根因，对齐 LocalizationTests.LoadAssemblies 先例）
        RuntimeHelpers.RunModuleConstructor(typeof(global::Stardust.StarClient).Module.ModuleHandle);
        RuntimeHelpers.RunModuleConstructor(typeof(global::StarAgent.StarService).Module.ModuleHandle);
        RuntimeHelpers.RunModuleConstructor(typeof(global::StarAgent.Mobile.MobileAgent).Module.ModuleHandle);
        RuntimeHelpers.RunModuleConstructor(typeof(global::Microsoft.Extensions.DependencyInjection.StarFactoryExtensions).Module.ModuleHandle);
        // 本测试断言键「应用鉴权」注册在 Data 词表（源生成器编译期解析 Locales/*.yml；测试工程仅引用 Data/Client/Agent 面，Server/Web 词表不在进程），补触 Data 模块构造器
        RuntimeHelpers.RunModuleConstructor(typeof(global::Stardust.Data.HistoryAction).Module.ModuleHandle);

        var oldCulture = Localization.Culture;
        var oldLog = Localization.LogCulture;
        try
        {
            // Stardust.Data 的词表随程序集加载注册，先触碰类型避免首次查表时尚未注册
            GC.KeepAlive(typeof(Stardust.Data.Nodes.Node));

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
