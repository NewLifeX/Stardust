using System.Collections;
using System.Reflection;
using Stardust.Data.Monitors;
using Stardust.Server.Services;
using Xunit;

namespace ServerTest.Services;

/// <summary>追踪统计服务单元测试。验证热门窗口门控、内存查询与分层周期的关键行为</summary>
public class TraceStatServiceTests
{
    /// <summary>创建关闭定时器的统计服务，避免测试期间触发后台任务与数据库访问</summary>
    private static TraceStatService CreateService() => new(null)
    {
        FlowPeriod = 0,
        BatchPeriod = 0,
    };

    private static TraceData CreateTrace(Int32 appId, String name)
    {
        var now = DateTime.Now;
        return new TraceData
        {
            AppId = appId,
            Name = name,
            StatDate = now.Date,
            StatHour = now.Date.AddHours(now.Hour),
            StatMinute = now.Date.AddHours(now.Hour).AddMinutes(now.Minute / 5 * 5),
        };
    }

    private static Object GetField(Object target, String name)
    {
        var field = typeof(TraceStatService).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(field);

        return field.GetValue(target);
    }

    /// <summary>非热门应用不登记小时/日包，只保留分钟包与当天活跃登记，交由巡检按周期重算</summary>
    [Fact]
    [Trait("Category", "Monitor")]
    public void Add_NonHotApp_OnlyMinuteBag()
    {
        var svc = CreateService();
        svc.Add([CreateTrace(101, "api/test")]);

        Assert.Empty((IEnumerable)GetField(svc, "_bagDay"));
        Assert.Empty((IEnumerable)GetField(svc, "_bagHour"));
        Assert.NotEmpty((IEnumerable)GetField(svc, "_bagMinute"));
        Assert.NotEmpty((IEnumerable)GetField(svc, "_dayActive"));
    }

    /// <summary>热门应用保持事件驱动重算，上报数据时登记小时与日包</summary>
    [Fact]
    [Trait("Category", "Monitor")]
    public void Add_HotApp_RegistersAllBags()
    {
        var svc = CreateService();

        // 直接注入热窗口标记，避免 SetHotApp 内部的手动重算登记影响断言
        ((IDictionary)GetField(svc, "_hotApps"))[102] = DateTime.Now;

        svc.Add([CreateTrace(102, "api/test")]);

        Assert.NotEmpty((IEnumerable)GetField(svc, "_bagDay"));
        Assert.NotEmpty((IEnumerable)GetField(svc, "_bagHour"));
        Assert.NotEmpty((IEnumerable)GetField(svc, "_bagMinute"));
    }

    /// <summary>设置热门应用时立即登记重算，保证打开监控页面后分钟槽、小时与当天统计即时刷新</summary>
    [Fact]
    [Trait("Category", "Monitor")]
    public void SetHotApp_RegistersRecompute()
    {
        var svc = CreateService();
        svc.SetHotApp(104);

        Assert.True(((IDictionary)GetField(svc, "_hotApps")).Contains(104));
        Assert.NotEmpty((IEnumerable)GetField(svc, "_bagDay"));
        Assert.NotEmpty((IEnumerable)GetField(svc, "_bagHour"));
        Assert.NotEmpty((IEnumerable)GetField(svc, "_bagMinute"));
    }

    /// <summary>手动补算（如网页重新统计）不受热门门控影响，始终登记小时/日包</summary>
    [Fact]
    [Trait("Category", "Monitor")]
    public void AddManually_NonHotApp_RegistersAllBags()
    {
        var svc = CreateService();
        svc.Add(103, DateTime.Now);

        Assert.NotEmpty((IEnumerable)GetField(svc, "_bagDay"));
        Assert.NotEmpty((IEnumerable)GetField(svc, "_bagHour"));
        Assert.NotEmpty((IEnumerable)GetField(svc, "_bagMinute"));
    }

    /// <summary>内存查询与紧急落盘在队列未初始化时安全返回，不抛异常</summary>
    [Fact]
    [Trait("Category", "Monitor")]
    public void LiveQuery_And_Flush_NullSafe()
    {
        var svc = CreateService();

        Assert.Null(svc.GetLiveMinute(1, 2, DateTime.Now));
        Assert.Null(svc.GetLiveAppMinute(1, DateTime.Now));

        svc.TriggerFlush();
        svc.Flush();
    }

    /// <summary>周期默认值。落盘放宽到300秒，巡检600秒（活跃窗口为巡检周期3倍）</summary>
    [Fact]
    [Trait("Category", "Monitor")]
    public void Defaults_MonitorPeriods()
    {
        var svc = new TraceStatService(null);

        Assert.Equal(300, svc.SavePeriod);
        Assert.Equal(600, svc.PatrolPeriod);
    }

    /// <summary>启动恢复补算默认开启；查看端进程可关闭，避免与数据上报进程重复重算</summary>
    [Fact]
    [Trait("Category", "Monitor")]
    public void EnableRecovery_Default_True()
    {
        var svc = new TraceStatService(null);

        Assert.True(svc.EnableRecovery);

        svc.EnableRecovery = false;
        Assert.False(svc.EnableRecovery);
    }

    /// <summary>巡检周期误配为非正数时回退默认周期，避免每个批周期都巡检造成写放大</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [Trait("Category", "Monitor")]
    public void Patrol_NonPositivePeriod_FallsBackToDefault(Int32 period)
    {
        var svc = new TraceStatService(null) { PatrolPeriod = period };

        var before = DateTime.Now;
        var method = typeof(TraceStatService).GetMethod("DoPatrol", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(method);
        method.Invoke(svc, null);

        // 下次巡检时间按默认600秒推进，允许执行耗时的小幅误差
        var next = (DateTime)GetField(svc, "_nextPatrol");
        Assert.True(next >= before.AddSeconds(595), $"next={next:HH:mm:ss.fff} before={before:HH:mm:ss.fff}");
        Assert.True(next <= DateTime.Now.AddSeconds(605));
    }

    /// <summary>巡检按周期节流：未到下次时间时重复调用不推进</summary>
    [Fact]
    [Trait("Category", "Monitor")]
    public void Patrol_NormalPeriod_Throttles()
    {
        var svc = new TraceStatService(null) { PatrolPeriod = 60 };

        var method = typeof(TraceStatService).GetMethod("DoPatrol", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(method);
        method.Invoke(svc, null);

        var next1 = (DateTime)GetField(svc, "_nextPatrol");
        Assert.True(next1 > DateTime.Now.AddSeconds(50));

        // 未到下次时间，重复调用不再推进
        method.Invoke(svc, null);
        var next2 = (DateTime)GetField(svc, "_nextPatrol");
        Assert.Equal(next1, next2);
    }

    /// <summary>告警辅助函数：错误率现场计算；内存值优先于数据库值，数据库值更新时取数据库值</summary>
    [Fact]
    [Trait("Category", "Monitor")]
    public void AlarmHelpers_CalcErrorRate_And_SelectLatest()
    {
        var alarmType = typeof(AlarmService);
        var calc = alarmType.GetMethod("CalcErrorRate", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(calc);
        Assert.Equal(0D, (Double)calc.Invoke(null, [0L, 5L]));
        Assert.Equal(0.1D, (Double)calc.Invoke(null, [100L, 10L]));

        var select = alarmType.GetMethod("SelectLatest", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(select);

        var live = new TraceMinuteStat { Total = 100, Errors = 10 };
        var db = new TraceMinuteStat { Total = 50, Errors = 5 };

        Assert.Same(live, select.Invoke(null, [live, db]));
        Assert.Same(live, select.Invoke(null, [live, null]));
        Assert.Same(db, select.Invoke(null, [null, db]));

        // 数据库值更新（错误数更大）时取数据库值
        var db2 = new TraceMinuteStat { Total = 200, Errors = 20 };
        Assert.Same(db2, select.Invoke(null, [live, db2]));
    }
}
