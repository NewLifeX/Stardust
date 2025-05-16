using System.Collections.Concurrent;
using NewLife.Log;
using NewLife.Threading;
using Stardust.Data.Monitors;

namespace Stardust.Server.Services;

/// <summary>跟踪项统计服务</summary>
public interface ITraceItemStatService
{
    /// <summary>添加需要统计的应用，去重</summary>
    /// <param name="appId"></param>
    void Add(Int32 appId);

    /// <summary>统计指定应用</summary>
    /// <param name="appId"></param>
    /// <param name="days"></param>
    void Process(Int32 appId, Int32 days);
}

/// <summary>跟踪项统计服务</summary>
public class TraceItemStatService : ITraceItemStatService
{
    /// <summary>批计算周期。默认600秒</summary>
    public Int32 BatchPeriod { get; set; } = 600;

    private TimerX _timer;
    private readonly ConcurrentBag<Int32> _bag = [];
    private readonly ITracer _tracer;

    public TraceItemStatService(ITracer tracer) => _tracer = tracer;

    /// <summary>添加需要统计的应用，去重</summary>
    /// <param name="appId"></param>
    public void Add(Int32 appId)
    {
        if (!_bag.Contains(appId)) _bag.Add(appId);

        // 初始化定时器
        if (_timer == null && BatchPeriod > 0)
        {
            lock (this)
            {
                if (_timer == null && BatchPeriod > 0) _timer = new TimerX(DoTraceStat, null, 1_000, BatchPeriod * 1000) { Async = true };
            }
        }

        //if (_timer.NextTime > DateTime.Now.AddSeconds(5)) _timer.SetNext(-1);
    }

    private void DoTraceStat(Object state)
    {
        while (_bag.TryTake(out var appId))
        {
            using var span = _tracer?.NewSpan("TraceItemStat-Process", appId);
            try
            {
                Process(appId, 7);
            }
            catch (Exception ex)
            {
                span.SetError(ex, null);
                throw;
            }
        }
    }

    public void Process(Int32 appId, Int32 days = 7)
    {
        var startTime = DateTime.Today.AddDays(-days);

        var app = AppTracer.FindByID(appId);
        var list = TraceItem.GetValids(appId, startTime);
        var sts = TraceDayStat.SearchGroupItemByApp(appId, startTime);
        foreach (var st in sts)
        {
            var ti = list.FirstOrDefault(e => e.Id == st.ItemId);
            if (ti == null && st.ItemId > 0) ti = TraceItem.FindById(st.ItemId);

            // 只统计能找到的跟踪项
            if (ti != null)
            {
                ti.Days = st.ID;
                ti.Total = st.Total;
                ti.Errors = st.Errors;
                ti.Cost = st.Cost;
                ti.Update();
            }
        }
    }
}