extern alias web;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using Stardust.Data.Monitors;
using Xunit;
using web::Stardust.Web.Areas.Monitors.Charts;

namespace ServerTest.Monitors;

/// <summary>应用分钟统计监控大屏图表测试</summary>
/// <remarks>
/// 仓库并存两个同简单名 NewLife.Cube 程序集：Stardust.Web 用 NewLife.Cube.Core（完整版，含 ECharts，
/// 程序集版本 6.16.9799.11704），Stardust.WebApi 用 NewLife.Cube（API 版，无 ECharts，6.16.9799.11718）。
/// ServerTest 同时传递引用两者，编译器统一取高修订版 11718（API 版）→ 编译期无法绑定 ECharts 元数据
/// （CS7069）。因此 BuildDashboard 与 chart.Build() 改为反射调用，摆脱编译期类型绑定；
/// 运行期由 ServerTest.csproj 的 UseFullCubeForCharts 目标把 Core 版 dll 覆盖进输出目录。
/// 上游统一两包程序集版本后，可恢复直接调用并移除该目标。
/// </remarks>
public class AppMinuteStatChartTests
{
    [Fact]
    [DisplayName("监控大屏图表使用 axis+shadow 提示且可正常序列化")]
    public void BuildDashboard_UsesAxisShadowTooltip()
    {
        var list = new List<AppMinuteStat>
        {
            new() { AppId = 1, Total = 100, Errors = 5 },
            new() { AppId = 2, Total = 50, Errors = 0 },
        };

        var buildDashboard = typeof(AppMinuteStatChart).GetMethod("BuildDashboard", BindingFlags.Public | BindingFlags.Static);
        Assert.NotNull(buildDashboard);
        var chart = buildDashboard!.Invoke(null, [list]);
        Assert.NotNull(chart);

        var build = chart!.GetType().GetMethod("Build", Type.EmptyTypes);
        Assert.NotNull(build);
        var json = Assert.IsType<String>(build!.Invoke(chart, null));

        // Cube 6.16 的 SetTooltip(trigger, axisPointerType) 与 SetTooltip(trigger, formatterScript) 重载冲突，
        // 旧代码传两个位置参数会命中 formatter 重载并抛 ArgumentOutOfRangeException
        Assert.Contains("\"trigger\":\"axis\"", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"type\":\"shadow\"", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("formatter", json, StringComparison.OrdinalIgnoreCase);
    }
}
