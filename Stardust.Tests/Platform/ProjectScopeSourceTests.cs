using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace Stardust.Tests.Platform;

/// <summary>项目范围源码防漏测试。HTTP 可达代码的缓存查找必须经项目可见性校验</summary>
public class ProjectScopeSourceTests
{
    private static readonly Regex _cachedFind = new(@"\.(?<method>FindById|FindByID|FindByName|FindByCode|FindBySecret|FindByKey)\s*\(", RegexOptions.Compiled);
    private static readonly Regex _listFind = new(@"\.FindAllBy\w+\s*\(", RegexOptions.Compiled);
    private static readonly Regex _staticFind = new(@"(?<!\.)\b(FindByID|FindById|FindByName|FindByCode|FindByKey)\s*\(", RegexOptions.Compiled);

    [Fact(DisplayName = "HTTP 可达代码的缓存查找统一经项目可见性校验")]
    public void CachedLookupUsesProjectGuard()
    {
        var root = FindRepoRoot();
        Assert.NotNull(root);

        // 看板与启动期后台任务自行处理范围；MCP 令牌守卫读取的是令牌自身资源
        var allow = new HashSet<String>(StringComparer.OrdinalIgnoreCase)
        {
            "DashboardService.cs",
            "FixDataHostedService.cs",
            "McpGuard.cs",
        };

        var hits = new List<String>();
        foreach (var dir in GetHttpDirs(root))
        {
            if (!Directory.Exists(dir)) continue;

            foreach (var file in Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories))
            {
                if (allow.Contains(Path.GetFileName(file))) continue;

                var lineNo = 0;
                foreach (var line in File.ReadAllLines(file))
                {
                    lineNo++;
                    var text = line.TrimStart();
                    if (text.StartsWith("//") || text.StartsWith("///")) continue;
                    if (line.Contains("ProjectScopeRules.FilterVisible")) continue;
                    var staticHit = _staticFind.IsMatch(line) && !line.Contains(" static ");
                    if (!_cachedFind.IsMatch(line) && !_listFind.IsMatch(line) && !staticHit) continue;

                    hits.Add($"{Path.GetRelativePath(root, file)}:{lineNo}: {line.Trim()}");
                }
            }
        }

        Assert.True(hits.Count == 0, "发现未经过项目可见性校验的缓存查找：\n" + String.Join("\n", hits));
    }

    private static String[] GetHttpDirs(String root) =>
    [
        Path.Combine(root, "Stardust.Web", "Controllers"),
        Path.Combine(root, "Stardust.WebApi", "Controllers"),
        Path.Combine(root, "Stardust.Web", "Areas"),
        Path.Combine(root, "Stardust.WebApi", "Areas"),
        Path.Combine(root, "Stardust.Web", "Services"),
        Path.Combine(root, "Stardust.WebApi", "Services"),
        Path.Combine(root, "Stardust.Mcp"),
    ];

    private static String FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (var i = 0; dir != null && i < 10; i++, dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "星尘.slnx"))) return dir.FullName;
        }

        return null;
    }
}