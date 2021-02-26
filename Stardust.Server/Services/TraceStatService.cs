using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using NewLife;
using NewLife.Log;
using NewLife.Threading;
using Stardust.Data.Monitors;
using XCode;
using XCode.Model;

namespace Stardust.Server.Services
{
    /// <summary>跟踪统计服务</summary>
    public interface ITraceStatService
    {
        /// <summary>添加需要统计的跟踪数据</summary>
        /// <param name="traces"></param>
        void Add(IList<TraceData> traces);

        /// <summary>统计特定应用和时间</summary>
        /// <param name="appId"></param>
        /// <param name="time"></param>
        void Add(Int32 appId, DateTime time);
    }

    /// <summary>跟踪统计服务</summary>
    public class TraceStatService : ITraceStatService
    {
        /// <summary>流计算周期。默认5秒</summary>
        public Int32 FlowPeriod { get; set; } = 5;

        /// <summary>批计算周期。默认30秒</summary>
        public Int32 BatchPeriod { get; set; } = 30;

        private TimerX _timerFlow;
        private TimerX _timerBatch;
        private readonly ConcurrentBag<String> _bagDay = new ConcurrentBag<String>();
        private readonly ConcurrentBag<String> _bagHour = new ConcurrentBag<String>();
        private readonly ConcurrentDictionary<String, ConcurrentBag<DateTime>> _bagMinute = new ConcurrentDictionary<String, ConcurrentBag<DateTime>>();
        private readonly ConcurrentQueue<TraceData> _queue = new ConcurrentQueue<TraceData>();

        /* 延迟队列技术 */
        private readonly DayQueue _dayQueue = new DayQueue { Period = 60 };
        private readonly HourQueue _hourQueue = new HourQueue { Period = 60 };
        private readonly MinuteQueue _minuteQueue = new MinuteQueue { Period = 60 };
        private readonly AppMinuteQueue _appMinuteQueue = new AppMinuteQueue { Period = 60 };

        private Int32 _count;
        private readonly ITracer _tracer;

        public TraceStatService(ITracer tracer) => _tracer = tracer;

        /// <summary>添加需要统计的跟踪数据</summary>
        /// <param name="traces"></param>
        public void Add(IList<TraceData> traces)
        {
            if (traces == null || traces.Count == 0) return;

            foreach (var item in traces)
            {
                {
                    var key = $"{ item.AppId}_{item.StatDate.ToFullString()}";
                    if (!_bagDay.Contains(key)) _bagDay.Add(key);
                }
                {
                    var key = $"{ item.AppId}_{item.StatHour.ToFullString()}";
                    if (!_bagHour.Contains(key)) _bagHour.Add(key);
                }
                {
                    var key = $"{item.AppId}_{item.StatMinute:yyyyMMddHH}";
                    var bag = _bagMinute.GetOrAdd(key, new ConcurrentBag<DateTime>());
                    if (!bag.Contains(item.StatMinute)) bag.Add(item.StatMinute);
                }
            }

            if (_count > 100_000) return;

            // 加入队列，增量计算
            foreach (var item in traces)
            {
                _queue.Enqueue(item);
                Interlocked.Increment(ref _count);
            }

            // 初始化定时器，用于流式增量计算和批量计算
            Init();
        }

        /// <summary>统计特定应用和时间</summary>
        /// <param name="appId"></param>
        /// <param name="time"></param>
        public void Add(Int32 appId, DateTime time)
        {
            Init();

            {
                var key = $"{appId}_{time.Date.ToFullString()}";
                if (!_bagDay.Contains(key)) _bagDay.Add(key);
            }
            {
                var hour = time.Date.AddHours(time.Hour);
                var key = $"{appId}_{hour.ToFullString()}";
                if (!_bagHour.Contains(key)) _bagHour.Add(key);
            }
            {
                var minute = time.Date.AddHours(time.Hour).AddMinutes(time.Minute / 5 * 5);
                var key = $"{appId}_{minute:yyyyMMddHH}";
                var bag = _bagMinute.GetOrAdd(key, new ConcurrentBag<DateTime>());
                if (!bag.Contains(minute)) bag.Add(minute);
            }

            _timerBatch?.SetNext(3_000);
        }

        /// <summary>初始化定时器</summary>
        public void Init()
        {
            if (_timerFlow == null && FlowPeriod > 0)
            {
                lock (this)
                {
                    if (_timerFlow == null) _timerFlow = new TimerX(DoFlowStat, null, 5_000, FlowPeriod * 1000) { Async = true };
                }
            }
            if (_timerBatch == null && BatchPeriod > 0)
            {
                lock (this)
                {
                    if (_timerBatch == null) _timerBatch = new TimerX(DoBatchStat, null, 5_000, BatchPeriod * 1000) { Async = true };
                }
            }
        }

        /// <summary>流式计算，增量累加</summary>
        /// <param name="state"></param>
        private void DoFlowStat(Object state)
        {
            if (_queue.IsEmpty) return;

            using var span = _tracer?.NewSpan("TraceFlowStat");

            // 限制每次只处理这么多
            var count = 100_000;
            while (count-- > 0)
            {
                if (!_queue.TryDequeue(out var td)) break;
                Interlocked.Decrement(ref _count);

                // 过滤异常数据
                if (td.AppId <= 0 || td.Name.IsNullOrEmpty()) continue;

                // 每日
                {
                    var st = _dayQueue.GetOrAdd(td.StatDate, td.AppId, td.Name, out var key);

                    st.Total += td.Total;
                    st.Errors += td.Errors;
                    st.TotalCost += td.TotalCost;
                    if (st.MaxCost < td.MaxCost) st.MaxCost = td.MaxCost;
                    if (st.MinCost <= 0 || st.MinCost > td.MinCost && td.MinCost > 0) st.MinCost = td.MinCost;

                    _dayQueue.Commit(key);
                }

                // 小时
                {
                    var st = _hourQueue.GetOrAdd(td.StatHour, td.AppId, td.Name, out var key);

                    st.Total += td.Total;
                    st.Errors += td.Errors;
                    st.TotalCost += td.TotalCost;
                    if (st.MaxCost < td.MaxCost) st.MaxCost = td.MaxCost;
                    if (st.MinCost <= 0 || st.MinCost > td.MinCost && td.MinCost > 0) st.MinCost = td.MinCost;

                    _hourQueue.Commit(key);
                }

                // 分钟
                {
                    var st = _minuteQueue.GetOrAdd(td.StatMinute, td.AppId, td.Name, out var key);

                    st.Total += td.Total;
                    st.Errors += td.Errors;
                    st.TotalCost += td.TotalCost;
                    if (st.MaxCost < td.MaxCost) st.MaxCost = td.MaxCost;
                    if (st.MinCost <= 0 || st.MinCost > td.MinCost && td.MinCost > 0) st.MinCost = td.MinCost;

                    _minuteQueue.Commit(key);
                }

                // 应用分钟
                {
                    var st = _appMinuteQueue.GetOrAdd(td.StatMinute, td.AppId, out var key);

                    st.Total += td.Total;
                    st.Errors += td.Errors;
                    st.TotalCost += td.TotalCost;
                    if (st.MaxCost < td.MaxCost) st.MaxCost = td.MaxCost;
                    if (st.MinCost <= 0 || st.MinCost > td.MinCost && td.MinCost > 0) st.MinCost = td.MinCost;

                    _appMinuteQueue.Commit(key);
                }
            }
        }

        /// <summary>批计算，覆盖缺失</summary>
        /// <param name="state"></param>
        private void DoBatchStat(Object state)
        {
            var keys = _bagMinute.Keys;
            foreach (var item in keys)
            {
                // 摘取下来
                if (_bagMinute.TryRemove(item, out var bag))
                {
                    var ss = item.Split("_");
                    var appId = ss[0].ToInt();
                    var list = new List<DateTime>();
                    while (bag.TryTake(out var dt))
                    {
                        if (!list.Contains(dt)) list.Add(dt);
                    }

                    // 批量处理该应用，取最小时间和最大时间
                    if (list.Count > 0) ProcessMinute(appId, list.Min(), list.Max());
                }
            }

            // 休息5000ms，让分钟统计落库
            Thread.Sleep(5000);

            while (_bagHour.TryTake(out var key))
            {
                var ss = key.Split("_");
                ProcessHour(ss[0].ToInt(), ss[1].ToDateTime());
            }
            while (_bagDay.TryTake(out var key))
            {
                var ss = key.Split("_");
                ProcessDay(ss[0].ToInt(), ss[1].ToDateTime());
            }
        }

        private void ProcessDay(Int32 appId, DateTime time)
        {
            if (appId <= 0 || time.Year < 2000) return;

            var date = time.Date;
            using var span = _tracer?.NewSpan("TraceBatchtat-Day", time);

            // 统计数据。分钟级统计可能因埋点名称污染，导致产生大量数据，这里过滤最要最大1000行
            var list = TraceMinuteStat.FindAllByAppIdWithCache(appId, date, 24 * 60 / 5 * 1000);
            if (list.Count == 0) return;

            // 聚合
            // 分组聚合，这里包含了每个接口在该日内的所有分钟统计，需要求和
            foreach (var item in list.GroupBy(e => e.Name))
            {
                var name = item.Key;
                if (name.IsNullOrEmpty()) continue;

                var st = _dayQueue.GetOrAdd(date, appId, name, out var key);

                var vs = item.ToList();
                st.Total = vs.Sum(e => e.Total);
                st.Errors = vs.Sum(e => e.Errors);
                st.TotalCost = vs.Sum(e => e.TotalCost);
                st.MaxCost = vs.Max(e => e.MaxCost);
                var vs2 = vs.Where(e => e.MinCost > 0).ToList();
                if (vs2.Count > 0) st.MinCost = vs2.Min(e => e.MinCost);

                // 强制触发种类计算
                st.Valid(false);

                _dayQueue.Commit(key);
            }
        }

        private void ProcessHour(Int32 appId, DateTime time)
        {
            if (appId <= 0 || time.Year < 2000) return;

            using var span = _tracer?.NewSpan("TraceBatchStat-Hour", time);
            time = time.Date.AddHours(time.Hour);

            // 统计数据。分钟级统计可能因埋点名称污染，导致产生大量数据，这里过滤最要最大1000行
            var list = TraceMinuteStat.FindAllByAppIdWithCache(appId, time.Date, 24 * 60 / 5 * 1000);
            list = list.Where(e => e.StatTime >= time & e.StatTime < time.AddHours(1)).ToList();
            if (list.Count == 0) return;

            // 分组聚合，这里包含了每个接口在该小时内的所有分钟统计，需要求和
            foreach (var item in list.GroupBy(e => e.Name))
            {
                var name = item.Key;
                if (name.IsNullOrEmpty()) continue;

                var st = _hourQueue.GetOrAdd(time, appId, name, out var key);

                var vs = item.ToList();
                st.Total = vs.Sum(e => e.Total);
                st.Errors = vs.Sum(e => e.Errors);
                st.TotalCost = vs.Sum(e => e.TotalCost);
                st.MaxCost = vs.Max(e => e.MaxCost);
                var vs2 = vs.Where(e => e.MinCost > 0).ToList();
                if (vs2.Count > 0) st.MinCost = vs2.Min(e => e.MinCost);

                _hourQueue.Commit(key);
            }
        }

        private void ProcessMinute(Int32 appId, DateTime start, DateTime end)
        {
            if (appId <= 0 || start.Year < 2000 || end.Year < 2000) return;

            using var span = _tracer?.NewSpan("TraceBatchStat-Minute", $"{start.ToFullString()}-{end.ToFullString()}");

            // 排除项
            var app = AppTracer.FindByID(appId);
            var excludes = app.Excludes.Split(",", ";") ?? new String[0];

            start = start.Date.AddHours(start.Hour).AddMinutes(start.Minute / 5 * 5);
            end = end.Date.AddHours(end.Hour).AddMinutes(end.Minute / 5 * 5);

            // 统计数据
            var list = TraceData.SearchGroupAppAndName(appId, start, end);
            list = list.Where(e => !e.Name.IsNullOrEmpty()).ToList();
            if (list.Count == 0) return;

            // 剔除指定项
            list = list.Where(e => !e.Name.IsNullOrEmpty() && !excludes.Any(y => y.IsMatch(e.Name))).ToList();

            // 聚合
            foreach (var item in list)
            {
                var st = _minuteQueue.GetOrAdd(item.StatMinute, appId, item.Name, out var key);

                st.Total = item.Total;
                st.Errors = item.Errors;
                st.TotalCost = item.TotalCost;
                st.MaxCost = item.MaxCost;
                st.MinCost = item.MinCost;

                _minuteQueue.Commit(key);
            }

            // 聚合应用分钟统计
            foreach (var item in list.GroupBy(e => e.AppId + "#" + e.StatMinute))
            {
                var traces = item.ToList();
                var st = _appMinuteQueue.GetOrAdd(traces[0].StatMinute, traces[0].AppId, out var key);

                st.Total = traces.Sum(e => e.Total);
                st.Errors = traces.Sum(e => e.Errors);
                st.TotalCost = traces.Sum(e => e.TotalCost);
                st.MaxCost = traces.Max(e => e.MaxCost);
                //st.MinCost = traces.Min(e => e.MinCost);
                var vs2 = traces.Where(e => e.MinCost > 0).ToList();
                if (vs2.Count > 0) st.MinCost = vs2.Min(e => e.MinCost);

                _appMinuteQueue.Commit(key);
            }
        }
    }

    class DayQueue : MyQueue
    {
        public TraceDayStat GetOrAdd(DateTime date, Int32 appId, String name, out String key)
        {
            var model = new TraceStatModel { Time = date, AppId = appId, Name = name };
            key = model.Key;
            return GetOrAdd(key, k => TraceDayStat.FindOrAdd(model));
        }
    }

    class HourQueue : MyQueue
    {
        public TraceHourStat GetOrAdd(DateTime date, Int32 appId, String name, out String key)
        {
            var model = new TraceStatModel { Time = date, AppId = appId, Name = name };
            key = model.Key;
            return GetOrAdd(key, k => TraceHourStat.FindOrAdd(model));
        }
    }

    class MinuteQueue : MyQueue
    {
        public TraceMinuteStat GetOrAdd(DateTime date, Int32 appId, String name, out String key)
        {
            var model = new TraceStatModel { Time = date, AppId = appId, Name = name };
            key = model.Key;
            return GetOrAdd(key, k => TraceMinuteStat.FindOrAdd(model));
        }
    }

    class AppMinuteQueue : MyQueue
    {
        public AppMinuteStat GetOrAdd(DateTime date, Int32 appId, out String key)
        {
            var model = new TraceStatModel { Time = date, AppId = appId };
            key = model.Key;
            return GetOrAdd(key, k => AppMinuteStat.FindOrAdd(model));
        }
    }

    class MyQueue : EntityDeferredQueue
    {
        #region 方法
        /// <summary>处理一批</summary>
        /// <param name="list"></param>
        public override Int32 Process(IList<Object> list)
        {
            if (list.Count == 0) return 0;

            return list.Cast<IEntity>().Update();
        }
        #endregion
    }
}