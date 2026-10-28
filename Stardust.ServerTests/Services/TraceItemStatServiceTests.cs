using System;
using System.Collections.Concurrent;
using System.Reflection;
using System.Threading;
using NewLife;
using NewLife.Threading;
using Stardust.Data.Monitors;
using Stardust.Server.Services;
using Xunit;

namespace ServerTest.Services;

/// <summary>埋点项统计服务测试。覆盖入队去重、定时器开关与无数据时空跑（APM-5）</summary>
public class TraceItemStatServiceTests
{
    private static ConcurrentBag<Int32> GetBag(TraceItemStatService service)
    {
        var fi = typeof(TraceItemStatService).GetField("_bag", BindingFlags.NonPublic | BindingFlags.Instance);
        return (ConcurrentBag<Int32>)fi.GetValue(service);
    }

    private static TimerX GetTimer(TraceItemStatService service)
    {
        var fi = typeof(TraceItemStatService).GetField("_timer", BindingFlags.NonPublic | BindingFlags.Instance);
        return (TimerX)fi.GetValue(service);
    }

    [Fact(DisplayName = "同一应用重复入队只保留一条")]
    public void Add_Deduplicate()
    {
        var service = new TraceItemStatService(null) { BatchPeriod = 0 };
        try
        {
            service.Add(1001);
            service.Add(1001);
            service.Add(1002);

            var bag = GetBag(service);
            Assert.Equal(2, bag.Count);
            Assert.Contains(1001, bag);
            Assert.Contains(1002, bag);
        }
        finally
        {
            GetTimer(service)?.TryDispose();
        }
    }

    [Fact(DisplayName = "批计算周期为零时不启动定时器")]
    public void Add_NoTimer_WhenPeriodZero()
    {
        var service = new TraceItemStatService(null) { BatchPeriod = 0 };
        service.Add(2001);

        Assert.Null(GetTimer(service));
    }

    [Fact(DisplayName = "批计算周期为正时首次入队启动定时器")]
    public void Add_StartTimer_WhenPeriodPositive()
    {
        var service = new TraceItemStatService(null) { BatchPeriod = 3600 };
        try
        {
            service.Add(3001);

            Assert.NotNull(GetTimer(service));
        }
        finally
        {
            GetTimer(service)?.TryDispose();
        }
    }

    [Fact(DisplayName = "没有统计数据时处理空跑不抛异常")]
    public void Process_NoData_NoThrow()
    {
        var service = new TraceItemStatService(null);

        // 不存在应用编号，统计结果为空，不应抛异常
        service.Process(-99991, 1);
    }
}
