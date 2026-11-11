using System;
using System.Linq;
using Stardust.Web.Models.Dashboard;
using Xunit;

namespace Stardust.Tests.Dashboard;

/// <summary>看板聚合纯函数测试。覆盖健康度、错误率、容量解析与进程守护等派生逻辑</summary>
public class DashboardMathTests
{
    [Fact(DisplayName = "错误率：总量为零时返回零")]
    public void ErrorRate_ZeroTotal_ReturnsZero()
    {
        Assert.Equal(0, DashboardMath.ErrorRate(0, 0));
        Assert.Equal(0, DashboardMath.ErrorRate(5, 0));
    }

    [Fact(DisplayName = "错误率：按百分比计算并保留精度")]
    public void ErrorRate_ComputesPercent()
    {
        Assert.Equal(1, DashboardMath.ErrorRate(1, 100));
        Assert.Equal(0.25, DashboardMath.ErrorRate(1, 400), 6);
    }

    [Fact(DisplayName = "健康度：全部在线且无错误为满分")]
    public void Health_Perfect_Returns100()
    {
        Assert.Equal(100, DashboardMath.Health(4, 4, 8, 8, 0, 0));
    }

    [Fact(DisplayName = "健康度：节点全离线扣分且不低于零")]
    public void Health_AllOffline_Penalizes()
    {
        var score = DashboardMath.Health(4, 4, 8, 0, 0, 0);

        Assert.True(score <= 70, $"节点全离线应明显扣分，实际 {score}");
        Assert.InRange(score, 0, 100);
    }

    [Fact(DisplayName = "健康度：错误率与告警持续扣分但夹在 0-100")]
    public void Health_ErrorsAndAlarms_Clamped()
    {
        var score = DashboardMath.Health(2, 0, 2, 0, 9.9, 99);

        Assert.InRange(score, 0, 100);
    }

    [Fact(DisplayName = "健康等级：按阈值返回文本与配色")]
    public void HealthLevel_MapsThresholds()
    {
        Assert.Equal("优秀", DashboardMath.HealthLevel(98));
        Assert.Equal("优秀", DashboardMath.HealthLevel(90));
        Assert.Equal("良好", DashboardMath.HealthLevel(80));
        Assert.Equal("一般", DashboardMath.HealthLevel(60));
        Assert.Equal("较差", DashboardMath.HealthLevel(50));

        Assert.Equal("ok", DashboardMath.HealthTone(95));
        Assert.Equal("warn", DashboardMath.HealthTone(70));
        Assert.Equal("bad", DashboardMath.HealthTone(40));
    }

    [Fact(DisplayName = "资源水位：按 65/80 分档")]
    public void Level_MapsWatermarks()
    {
        Assert.Equal("ok", DashboardMath.Level(64.9));
        Assert.Equal("warn", DashboardMath.Level(65));
        Assert.Equal("bad", DashboardMath.Level(80));
    }

    [Fact(DisplayName = "资源告警：任一指标超过对应阈值即告警")]
    public void IsResourceAlarm_AnyExceeded()
    {
        Assert.False(DashboardMath.IsResourceAlarm(50, 60, 70, 90, 90, 90));
        Assert.True(DashboardMath.IsResourceAlarm(91, 60, 70, 90, 90, 90));
        Assert.True(DashboardMath.IsResourceAlarm(50, 95, 70, 90, 90, 90));
        Assert.True(DashboardMath.IsResourceAlarm(50, 60, 99, 90, 90, 90));
        // 阈值为 0 表示未配置，不参与判定
        Assert.False(DashboardMath.IsResourceAlarm(99, 99, 99, 0, 0, 0));
    }

    [Fact(DisplayName = "磁盘解析：识别盘符、文件系统与已用容量")]
    public void ParseDrives_ParsesFormat()
    {
        var list = DashboardMath.ParseDrives("/[ext4]=120G/500G,C:[NTFS]=200G/500G");

        Assert.Equal(2, list.Count);
        Assert.Equal("/", list[0].Name);
        Assert.Equal("ext4", list[0].Format);
        Assert.Equal(380, list[0].Used, 3);
        Assert.Equal(500, list[0].Total, 3);
        Assert.Equal(76, list[0].Usage, 3);

        Assert.Equal("C:", list[1].Name);
        Assert.Equal("NTFS", list[1].Format);
        Assert.Equal(60, list[1].Usage, 3);
    }

    [Fact(DisplayName = "磁盘解析：空值与脏数据返回空集合")]
    public void ParseDrives_InvalidInput_ReturnsEmpty()
    {
        Assert.Empty(DashboardMath.ParseDrives(null));
        Assert.Empty(DashboardMath.ParseDrives(""));
        Assert.Empty(DashboardMath.ParseDrives("garbage"));
        Assert.Empty(DashboardMath.ParseDrives("/=abc/def"));
    }

    [Fact(DisplayName = "容量解析：支持 K/M/G/T 与字节")]
    public void ParseSize_Units()
    {
        Assert.Equal(1, DashboardMath.ParseSize("1G"), 3);
        Assert.Equal(0.5, DashboardMath.ParseSize("512M"), 3);
        Assert.Equal(1024, DashboardMath.ParseSize("1T"), 3);
        Assert.Equal(1024d * 1024 / (1024d * 1024 * 1024), DashboardMath.ParseSize("1024K"), 6);
        Assert.Equal(0, DashboardMath.ParseSize(""));
    }

    [Fact(DisplayName = "进程守护：配置与在线进程取交集判定运行状态")]
    public void BuildGuard_MatchesOnline()
    {
        var list = DashboardMath.BuildGuard("java.exe,nginx.exe", "java.exe,redis-server.exe");

        Assert.Equal(2, list.Count);
        Assert.True(list.First(e => e.Name == "java.exe").Running);
        Assert.False(list.First(e => e.Name == "nginx.exe").Running);
    }

    [Fact(DisplayName = "进程守护：未配置时以在线进程作为清单")]
    public void BuildGuard_NoConfig_UsesOnline()
    {
        var list = DashboardMath.BuildGuard(null, "a,b,a");

        Assert.Equal(2, list.Count);
        Assert.All(list, e => Assert.True(e.Running));
    }

    [Fact(DisplayName = "健康雷达：六维得分与综合分在合理范围")]
    public void Radar_Scores()
    {
        var (indicators, values, score) = DashboardMath.Radar(20, 40, 60, 90, 50, 7);

        Assert.Equal(6, indicators.Length);
        Assert.Equal(6, values.Length);
        Assert.Equal(100, values[0]);  // CPU 20% 在健康区间
        Assert.Equal(100, values[1]);  // 内存 40%
        Assert.Equal(80, values[2]);   // 磁盘 60% 超出 10 点扣 20 分
        Assert.Equal(90, values[3]);   // 网络
        Assert.Equal(100, values[4]);  // 温度 50℃ 未超过 65℃ 不扣分
        Assert.Equal(100, values[5]);  // 稳定 7 天封顶
        Assert.Equal(95, score);       // (100+100+80+90+100+100)/6
    }

    [Fact(DisplayName = "健康雷达：高温与短在线才扣分")]
    public void Radar_PenalizesOnlyBeyondThreshold()
    {
        var (_, values, _) = DashboardMath.Radar(0, 0, 0, 100, 75, 0);

        Assert.Equal(100, values[0]);
        Assert.Equal(60, values[4]);   // 75℃ 扣 40 分
        Assert.Equal(60, values[5]);   // 刚上线保留 60 分基础分
    }

    [Fact(DisplayName = "数字缩写：万与亿")]
    public void Abbreviate_Scales()
    {
        Assert.Equal("999", DashboardMath.Abbreviate(999));
        Assert.Equal("1.3 万", DashboardMath.Abbreviate(12860));
        Assert.Equal("1.29 亿", DashboardMath.Abbreviate(128600000));
    }

    [Fact(DisplayName = "分桶平均：把长序列压缩到目标点数")]
    public void BucketAverage_Compresses()
    {
        var values = Enumerable.Range(0, 100).Select(e => (Double)e).ToArray();
        var result = DashboardMath.BucketAverage(values, 10);

        Assert.Equal(10, result.Length);
        Assert.Equal(4.5, result[0], 3);
        Assert.Equal(94.5, result[^1], 3);
        Assert.Empty(DashboardMath.BucketAverage([], 10));
    }

    [Fact(DisplayName = "时长格式化：覆盖秒、分、时、天")]
    public void Duration_Formats()
    {
        Assert.Equal("—", DashboardMath.Duration(0));
        Assert.Equal("45s", DashboardMath.Duration(45));
        Assert.Equal("2m 5s", DashboardMath.Duration(125));
        Assert.Equal("1h 2m", DashboardMath.Duration(3720));
        Assert.Equal("2d 3h", DashboardMath.Duration(2 * 86400 + 3 * 3600));
    }

    [Fact(DisplayName = "百分比：分母为零返回零")]
    public void Percent_ZeroTotal()
    {
        Assert.Equal(0, DashboardMath.Percent(1, 0));
        Assert.Equal(50, DashboardMath.Percent(1, 2));
    }
}
