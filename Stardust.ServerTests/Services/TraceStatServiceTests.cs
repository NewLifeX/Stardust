using System.Collections;
using System.Reflection;
using NewLife.Model;
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

    private static void SetField(Object target, String name, Object value)
    {
        var field = typeof(TraceStatService).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(field);

        field.SetValue(target, value);
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

    /// <summary>流式计算仅热门应用累加小时/日统计；非热门应用跳过小时/日，只保留分钟队列</summary>
    [Fact]
    [Trait("Category", "Monitor")]
    public void Flow_NonHotApp_SkipsHourAndDayQueues()
    {
        var svc = CreateService();
        svc.Add([CreateTrace(105, "api/test")]);

        var flow = typeof(TraceStatService).GetMethod("DoFlowStat", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(flow);
        flow.Invoke(svc, [null]);

        // 非热门应用不产生小时/日队列
        Assert.Null(GetField(svc, "_hourQueue"));
        Assert.Null(GetField(svc, "_dayQueue"));

        // 分钟与应用分钟队列保持全量流式（告警数据源）
        var minute = (DeferredQueue)GetField(svc, "_minuteQueue");
        var appMinute = (DeferredQueue)GetField(svc, "_appMinuteQueue");
        Assert.NotNull(minute);
        Assert.NotNull(appMinute);
        Assert.Single(minute.Entities);
        Assert.Single(appMinute.Entities);

        CleanupStats(105);
    }

    /// <summary>热门应用的小时/日统计保持流式实时累加</summary>
    [Fact]
    [Trait("Category", "Monitor")]
    public void Flow_HotApp_UpdatesHourAndDayQueues()
    {
        var svc = CreateService();

        // 直接注入热窗口标记，避免 SetHotApp 内部的手动登记影响
        ((IDictionary)GetField(svc, "_hotApps"))[106] = DateTime.Now;

        svc.Add([CreateTrace(106, "api/test")]);

        var flow = typeof(TraceStatService).GetMethod("DoFlowStat", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(flow);
        flow.Invoke(svc, [null]);

        var hour = (DeferredQueue)GetField(svc, "_hourQueue");
        var day = (DeferredQueue)GetField(svc, "_dayQueue");
        Assert.NotNull(hour);
        Assert.NotNull(day);
        Assert.Single(hour.Entities);
        Assert.Single(day.Entities);

        CleanupStats(106);
    }

    /// <summary>进程首次巡检登记上一小时收尾，留到下一轮巡检消费，等待分钟统计先按周期落盘</summary>
    [Fact]
    [Trait("Category", "Monitor")]
    public void Patrol_FirstRun_SchedulesPreviousHourFinalize()
    {
        var svc = new TraceStatService(null) { PatrolPeriod = 1 };
        var hour = DateTime.Now.Date.AddHours(DateTime.Now.Hour);

        var patrol = typeof(TraceStatService).GetMethod("DoPatrol", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(patrol);
        patrol.Invoke(svc, null);

        // 首个巡检登记上一小时，留到下一轮执行；允许测试恰好跨越小时边界的少量偏差
        var pending = (DateTime)GetField(svc, "_pendingHour");
        Assert.True(pending == hour.AddHours(-1) || pending == hour, $"pending={pending:yyyy-MM-dd HH:mm:ss} hour={hour:yyyy-MM-dd HH:mm:ss}");

        // 下一轮消费待收尾登记
        SetField(svc, "_nextPatrol", DateTime.Now.AddSeconds(-1));
        patrol.Invoke(svc, null);
        Assert.Equal(DateTime.MinValue, (DateTime)GetField(svc, "_pendingHour"));
    }

    /// <summary>跨天首个巡检推迟日终补算：小时收尾未落盘前保留历史日登记，收尾后的下一轮再移除</summary>
    [Fact]
    [Trait("Category", "Monitor")]
    public void Patrol_CrossDay_DefersDayFinalizeUntilHourFinalized()
    {
        var svc = new TraceStatService(null) { PatrolPeriod = 1 };

        const Int32 appId = 991601;
        var date = DateTime.Today.AddDays(-1);
        var lastHour = date.AddHours(23);
        ((IDictionary)GetField(svc, "_dayActive"))[$"{appId}_{date:yyyy-MM-dd HH:mm:ss}"] = DateTime.Now;
        SetField(svc, "_lastPatrolHour", lastHour);
        SetField(svc, "_nextPatrol", DateTime.Now.AddSeconds(-1));

        var patrol = typeof(TraceStatService).GetMethod("DoPatrol", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(patrol);
        patrol.Invoke(svc, null);

        // 跨天且上一小时待收尾：登记保留，等待下一轮收尾后再日终
        Assert.Single((IDictionary)GetField(svc, "_dayActive"));
        Assert.Equal(lastHour, (DateTime)GetField(svc, "_pendingHour"));

        // 下一轮：先收尾上一小时（该应用无分钟数据为空转），随后完成日终并移除登记
        SetField(svc, "_nextPatrol", DateTime.Now.AddSeconds(-1));
        patrol.Invoke(svc, null);

        Assert.Empty((IDictionary)GetField(svc, "_dayActive"));
        Assert.Equal(DateTime.MinValue, (DateTime)GetField(svc, "_pendingHour"));
    }

    /// <summary>小时收尾从分钟统计全量重算并立即落盘，随后跨天日终聚合到最终值</summary>
    [Fact]
    [Trait("Category", "Monitor")]
    public void Patrol_HourFinalize_RebuildsHourFromMinutes()
    {
        var svc = new TraceStatService(null) { PatrolPeriod = 1 };

        const Int32 appId = 991602;
        const Int32 itemId = 881002;
        var statTime = new DateTime(2026, 7, 16, 10, 0, 0);

        var minute = new TraceMinuteStat
        {
            AppId = appId,
            ItemId = itemId,
            Name = "test-finalize-" + Guid.NewGuid().ToString("n")[..8],
            StatTime = statTime,
            Total = 100,
            Errors = 2,
            TotalCost = 500,
            MaxCost = 50,
            MinCost = 1,
        };
        minute.Insert();

        try
        {
            ((IDictionary)GetField(svc, "_dayActive"))[$"{appId}_{statTime:yyyy-MM-dd HH:mm:ss}"] = DateTime.Now;
            SetField(svc, "_pendingHour", statTime);
            SetField(svc, "_nextPatrol", DateTime.Now.AddSeconds(-1));

            var patrol = typeof(TraceStatService).GetMethod("DoPatrol", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(patrol);
            patrol.Invoke(svc, null);

            // 收尾小时已由分钟统计重建并落盘
            var hour = TraceHourStat.FindAll(TraceHourStat._.AppId == appId).FirstOrDefault();
            Assert.NotNull(hour);
            Assert.Equal(statTime, hour.StatTime);
            Assert.Equal(100L, hour.Total);
            Assert.Equal(2L, hour.Errors);

            // 小时收尾落盘后执行日终补算，历史日登记收敛移除
            Assert.Empty((IDictionary)GetField(svc, "_dayActive"));
        }
        finally
        {
            // 落盘残留队列，保证测试数据可完整清理
            svc.Flush();
            CleanupStats(appId);
        }
    }

    /// <summary>清理指定应用的统计测试数据，避免污染共享测试库</summary>
    private static void CleanupStats(Int32 appId)
    {
        foreach (var e in TraceHourStat.FindAll(TraceHourStat._.AppId == appId)) e.Delete();
        foreach (var e in TraceDayStat.FindAll(TraceDayStat._.AppId == appId)) e.Delete();
        foreach (var e in TraceMinuteStat.FindAll(TraceMinuteStat._.AppId == appId)) e.Delete();
        foreach (var e in AppMinuteStat.FindAll(AppMinuteStat._.AppId == appId)) e.Delete();
    }
}
