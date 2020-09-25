using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using NewLife;
using NewLife.Threading;
using Stardust.Data.Monitors;
using XCode;

namespace Stardust.Server.Services
{
    /// <summary>跟踪统计服务</summary>
    public interface ITraceStatService
    {
        /// <summary>添加需要统计的跟踪数据</summary>
        /// <param name="traces"></param>
        void Add(IList<TraceData> traces);
    }

    /// <summary>跟踪统计服务</summary>
    public class TraceStatService : ITraceStatService
    {
        /// <summary>开始时间</summary>
        public DateTime StartTime { get; set; }

        private TimerX _timerFlow;
        private TimerX _timerBatch;
        private readonly ConcurrentBag<Int32> _bag = new ConcurrentBag<Int32>();
        private readonly ConcurrentQueue<TraceData> _queue = new ConcurrentQueue<TraceData>();
        private Int32 _count;

        /// <summary>添加需要统计的跟踪数据</summary>
        /// <param name="traces"></param>
        public void Add(IList<TraceData> traces)
        {
            if (traces == null || traces.Count == 0) return;

            var appId = traces[0].AppId;
            if (!_bag.Contains(appId)) _bag.Add(appId);

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

        /// <summary>初始化定时器</summary>
        public void Init()
        {
            if (_timerFlow == null)
            {
                lock (this)
                {
                    if (_timerFlow == null) _timerFlow = new TimerX(DoFlowStat, null, 5_000, 30_000) { Async = true };
                    if (_timerBatch == null) _timerBatch = new TimerX(DoBatchStat, null, 5_000, 300_000) { Async = true };
                }
            }
        }

        private void DoFlowStat(Object state)
        {
            if (_queue.IsEmpty) return;

            // 消费所有数据，完成统计
            var dayStats = new List<TraceDayStat>();
            var hourStats = new List<TraceHourStat>();
            var minuteStats = new List<TraceMinuteStat>();

            // 限制每次只处理这么多
            var count = 10000;
            while (count-- > 0)
            {
                if (!_queue.TryDequeue(out var td)) break;
                Interlocked.Decrement(ref _count);

                // 过滤异常数据
                if (td.Name.IsNullOrEmpty()) continue;

                // 每日
                {
                    var st = TraceDayStat.FindOrAdd(dayStats, td);
                    st.Total += td.Total;
                    st.Errors += td.Errors;
                    st.TotalCost += td.TotalCost;
                    if (st.MaxCost < td.MaxCost) st.MaxCost = td.MaxCost;
                    if (st.MinCost <= 0 || st.MinCost > td.MinCost && td.MinCost > 0) st.MinCost = td.MinCost;
                }

                // 小时
                {
                    var st = TraceHourStat.FindOrAdd(hourStats, td);
                    st.Total += td.Total;
                    st.Errors += td.Errors;
                    st.TotalCost += td.TotalCost;
                    if (st.MaxCost < td.MaxCost) st.MaxCost = td.MaxCost;
                    if (st.MinCost <= 0 || st.MinCost > td.MinCost && td.MinCost > 0) st.MinCost = td.MinCost;
                }

                // 分钟
                {
                    var st = TraceMinuteStat.FindOrAdd(minuteStats, td);
                    st.Total += td.Total;
                    st.Errors += td.Errors;
                    st.TotalCost += td.TotalCost;
                    if (st.MaxCost < td.MaxCost) st.MaxCost = td.MaxCost;
                    if (st.MinCost <= 0 || st.MinCost > td.MinCost && td.MinCost > 0) st.MinCost = td.MinCost;
                }
            }

            dayStats.Save(true);
            hourStats.Save(true);
            minuteStats.Save(true);
        }

        private void DoBatchStat(Object state)
        {
            // 拿到需要统计的应用
            var appIds = new List<Int32>();
            while (_bag.TryTake(out var id))
            {
                appIds.Add(id);
            }
            appIds = appIds.Distinct().ToList();
            //var appIds = AppTracer.FindAllWithCache().Select(e => e.ID).ToList();
            if (appIds.Count == 0) return;

            // 统计1分钟之前数据
            var time = DateTime.Now.AddMinutes(-1);

            ProcessDay(time, appIds);
            ProcessHour(time, appIds);
            ProcessMinute(time, appIds);
        }

        private void ProcessDay(DateTime time, IList<Int32> appIds)
        {
            var date = time.Date;

            // 统计对象
            var sts = TraceDayStat.Search(date, appIds.ToArray());

            // 逐个应用计算
            foreach (var appId in appIds)
            {
                // 统计数据
                var list = TraceData.SearchGroupAppAndName("day", date, new[] { appId });
                if (list.Count == 0) return;

                // 聚合
                foreach (var item in list)
                {
                    if (item.Name.IsNullOrEmpty()) continue;

                    var st = TraceDayStat.FindOrAdd(sts, item);
                    st.Total = item.Total;
                    st.Errors = item.Errors;
                    st.TotalCost = item.TotalCost;
                    st.MaxCost = item.MaxCost;
                    st.MinCost = item.MinCost;
                }
            }

            // 保存统计
            sts.Save(true);
        }

        private void ProcessHour(DateTime time, IList<Int32> appIds)
        {
            time = time.Date.AddHours(time.Hour);

            // 统计对象
            var sts = TraceHourStat.Search(time, appIds.ToArray());

            // 逐个应用计算
            foreach (var appId in appIds)
            {
                // 统计数据
                var list = TraceData.SearchGroupAppAndName("hour", time, new[] { appId });
                if (list.Count == 0) return;

                // 聚合
                foreach (var item in list)
                {
                    if (item.Name.IsNullOrEmpty()) continue;

                    var st = TraceHourStat.FindOrAdd(sts, item);
                    st.Total = item.Total;
                    st.Errors = item.Errors;
                    st.TotalCost = item.TotalCost;
                    st.MaxCost = item.MaxCost;
                    st.MinCost = item.MinCost;
                }
            }

            // 保存统计
            sts.Save(true);
        }

        private void ProcessMinute(DateTime time, IList<Int32> appIds)
        {
            time = time.Date.AddHours(time.Hour).AddMinutes(time.Minute / 5 * 5);

            // 统计对象
            var sts = TraceMinuteStat.Search(time, appIds.ToArray());

            // 逐个应用计算
            foreach (var appId in appIds)
            {
                // 统计数据
                var list = TraceData.SearchGroupAppAndName("minute", time, new[] { appId });
                if (list.Count == 0) return;

                // 聚合
                foreach (var item in list)
                {
                    if (item.Name.IsNullOrEmpty()) continue;

                    var st = TraceMinuteStat.FindOrAdd(sts, item);
                    st.Total = item.Total;
                    st.Errors = item.Errors;
                    st.TotalCost = item.TotalCost;
                    st.MaxCost = item.MaxCost;
                    st.MinCost = item.MinCost;
                }
            }

            // 保存统计
            sts.Save(true);
        }
    }
}