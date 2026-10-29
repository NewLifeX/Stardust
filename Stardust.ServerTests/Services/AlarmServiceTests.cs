using System;
using System.ComponentModel;
using System.Reflection;
using Stardust.Server.Services;
using Xunit;

namespace ServerTest.Services;

/// <summary>告警服务单元测试</summary>
public class AlarmServiceTests
{
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
}
