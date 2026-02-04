using System.Collections.Concurrent;
using NewLife.Log;
using NewLife.Threading;
using Stardust.Data.Monitors;
using XCode;

namespace Stardust.Server.Services;

/// <summary>应用统计服务</summary>
public interface IAppDayStatService
{
    /// <summary>添加需要统计的应用，去重</summary>
    /// <param name="date"></param>
    void Add(DateTime date);
}

/// <summary>应用统计服务</summary>
public class AppDayStatService : IAppDayStatService
{
    /// <summary>批计算周期。默认30秒</summary>
    public Int32 BatchPeriod { get; set; } = 30;

    private TimerX _timer;
    private readonly ConcurrentBag<DateTime> _bag = [];
    private readonly ITracer _tracer;

    public AppDayStatService(ITracer tracer) => _tracer = tracer;

    /// <summary>添加需要统计的应用，去重</summary>
    /// <param name="date"></param>
    public void Add(DateTime date)
    {
        if (!_bag.Contains(date)) _bag.Add(date);

        // 初始化定时器
        if (_timer == null && BatchPeriod > 0)
        {
            lock (this)
            {
                if (_timer == null && BatchPeriod > 0) _timer = new TimerX(DoTraceStat, null, 5_000, BatchPeriod * 1000) { Async = true };
            }
        }
    }

    private void DoTraceStat(Object state)
    {
        while (_bag.TryTake(out var time))
        {
            using var span = _tracer?.NewSpan("AppDayStat-Process", time.Date);
            try
            {
                Process(time.Date);
            }
            catch (Exception ex)
            {
                span.SetError(ex, null);
                throw;
            }
        }

        // 更新周期
        if (BatchPeriod > 0 && _timer != null) _timer.Period = BatchPeriod * 1000;
    }

    private void Process(DateTime date)
    {
        // 统计数据，每日追踪根据应用和类型分组
        var list = TraceDayStat.SearchGroupAppAndType(date);
        if (list.Count == 0) return;

        var ns = TraceDayStat.SearchGroupAppAndItem(date);

        // 统计对象
        var sts = AppDayStat.Search(date, null);
        var sts2 = AppDayStat.Search(date.AddDays(-1), null);

        // 聚合，按应用分组，每一组内每个类型一行
        var dic = list.GroupBy(e => e.AppId);
        foreach (var item in dic)
        {
            var appId = item.Key;
            if (appId <= 0) continue;

            var ds = item.ToList();
            var st = sts.FirstOrDefault(e => e.AppId == appId);
            if (st == null)
            {
                st = new AppDayStat { StatDate = date, AppId = appId };
                sts.Add(st);
            }

            st.Total = ds.Sum(e => e.Total);
            st.Errors = ds.Sum(e => e.Errors);
            st.TotalCost = ds.Sum(e => e.TotalCost);
            st.MaxCost = ds.Max(e => e.MaxCost);
            //st.MinCost = ds.Min(e => e.MinCost);
            var vs2 = ds.Where(e => e.MinCost > 0).ToList();
            if (vs2.Count > 0) st.MinCost = vs2.Min(e => e.MinCost);

            // 分类统计，应用有可能缺失某些类别
            st.Apis = ds.Where(e => e.Type == "api").Sum(e => e.Total);
            st.Https = ds.Where(e => e.Type == "http").Sum(e => e.Total);
            st.Dbs = ds.Where(e => e.Type == "db").Sum(e => e.Total);
            st.Mqs = ds.Where(e => e.Type == "mq").Sum(e => e.Total);
            st.Redis = ds.Where(e => e.Type == "redis").Sum(e => e.Total);
            st.Others = ds.Where(e => e.Type == "other").Sum(e => e.Total);

            // 计算埋点个数
            st.Names = ns.Where(e => e.AppId == appId).Count();

            // 计算环比
            var st2 = sts2.FirstOrDefault(e => e.AppId == appId);
            if (st2 != null) st.RingRate = st2.Total <= 0 ? 1 : Math.Round((Double)st.Total / st2.Total, 4);

            st.Save();
        }

        //// 保存统计
        //sts.Save(false);
    }
}