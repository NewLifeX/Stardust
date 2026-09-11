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
/// <remarks>实例化跟踪项统计服务</remarks>
/// <param name="tracer">跟踪器</param>
public class TraceItemStatService(ITracer tracer) : ITraceItemStatService
{
    /// <summary>批计算周期。默认1800秒</summary>
    public Int32 BatchPeriod { get; set; } = 1800;

    private TimerX _timer;
    private readonly ConcurrentBag<Int32> _bag = [];

    /// <summary>添加需要统计的应用，去重</summary>
    /// <param name="appId">应用编号</param>
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

    /// <summary>执行批计算。从 TraceDayStat 聚合生成 TraceItem 统计，按应用统计 7 天数据</summary>
    /// <param name="state">定时器状态参数</param>
    private void DoTraceStat(Object state)
    {
        while (_bag.TryTake(out var appId))
        {
            using var span = tracer?.NewSpan("TraceItemStat-Process", appId);
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

        //var app = AppTracer.FindByID(appId);
        var list = TraceItem.GetValids(appId, startTime);
        var sts = TraceDayStat.SearchGroupByItem(appId, startTime);
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