using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using Stardust.Server.Services;
using Xunit;

namespace ServerTest.Services;

/// <summary>告警服务单元测试</summary>
public class AlarmServiceTests
{
    private sealed record Sample(Double Rate);

    [Fact]
    [DisplayName("环比统计取上一个完整小时，避免整点未写满误报")]
    public void GetStatHour_UsesPreviousCompleteHour()
    {
        var method = typeof(AlarmService).GetMethod("GetStatHour", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        var hour = (DateTime)method.Invoke(null, [new DateTime(2026, 10, 29, 1, 5, 0)]);
        Assert.Equal(new DateTime(2026, 10, 29, 0, 0, 0), hour);

        // 跨天时回到前一天 23:00
        hour = (DateTime)method.Invoke(null, [new DateTime(2026, 10, 29, 0, 30, 0)]);
        Assert.Equal(new DateTime(2026, 10, 28, 23, 0, 0), hour);
    }

    [Fact]
    [DisplayName("环比阈值判断：高调用与调用量下滑分别命中")]
    public void IsRingRateAlarm_ChecksBothBounds()
    {
        var method = typeof(AlarmService).GetMethod("IsRingRateAlarm", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        Assert.Equal(true, method.Invoke(null, [3.0, 3.0, 0.1]));
        Assert.Equal(false, method.Invoke(null, [2.9, 3.0, 0.1]));
        Assert.Equal(true, method.Invoke(null, [0.09, 3.0, 0.1]));
        Assert.Equal(true, method.Invoke(null, [0.1, 3.0, 0.1]));
        Assert.Equal(false, method.Invoke(null, [0.11, 3.0, 0.1]));

        // 未配置阈值时不告警
        Assert.Equal(false, method.Invoke(null, [1.0, 0.0, 0.0]));
    }

    [Fact]
    [DisplayName("连续确认：次数不足或中间出现未超限采样都不告警")]
    public void IsSustained_RequiresConsecutiveSamples()
    {
        var samples = new List<Sample>
        {
            new(0.95),
            new(0.92),
            new(0.91),
        };

        Assert.True(AlarmService.IsSustained(samples, 3, e => e.Rate >= 0.9));

        // 次数不足
        Assert.False(AlarmService.IsSustained(samples, 4, e => e.Rate >= 0.9));
        Assert.False(AlarmService.IsSustained(new List<Sample>(), 1, e => e.Rate >= 0.9));

        // 中间出现一次回落，视为抖动
        samples[2] = new Sample(0.5);
        Assert.False(AlarmService.IsSustained(samples, 3, e => e.Rate >= 0.9));

        // 只确认最近一次时仍然成立
        Assert.True(AlarmService.IsSustained(samples, 1, e => e.Rate >= 0.9));
    }

    [Fact]
    [DisplayName("重复通知周期默认30分钟，持久故障不会每5分钟推送")]
    public void RenotifyPeriod_DefaultsToThirtyMinutes() => Assert.Equal(1800, AlarmService.DefaultRenotifyPeriod);
}
