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

        /// <summary>统计特定应用和时间</summary>
        /// <param name="appId"></param>
        /// <param name="time"></param>
        void Add(Int32 appId, DateTime time);
    }

    /// <summary>跟踪统计服务</summary>
    public class TraceStatService : ITraceStatService
    {
        /// <summary>开始时间</summary>
        public DateTime StartTime { get; set; }

        private TimerX _timerFlow;
        private TimerX _timerBatch;
        private readonly ConcurrentBag<String> _bagDay = new ConcurrentBag<String>();
        private readonly ConcurrentBag<String> _bagHour = new ConcurrentBag<String>();
        private readonly ConcurrentBag<String> _bagMinute = new ConcurrentBag<String>();
        private readonly ConcurrentQueue<TraceData> _queue = new ConcurrentQueue<TraceData>();
        private Int32 _count;

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
                    var key = $"{ item.AppId}_{item.StatMinute.ToFullString()}";
                    if (!_bagMinute.Contains(key)) _bagMinute.Add(key);
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
                var key = $"{appId}_{minute.ToFullString()}";
                if (!_bagMinute.Contains(key)) _bagMinute.Add(key);
            }

            _timerBatch?.SetNext(3_000);
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
            while (_bagDay.TryTake(out var key))
            {
                var ss = key.Split("_");
                ProcessDay(ss[0].ToInt(), ss[1].ToDateTime());
            }
            while (_bagHour.TryTake(out var key))
            {
                var ss = key.Split("_");
                ProcessHour(ss[0].ToInt(), ss[1].ToDateTime());
            }
            while (_bagMinute.TryTake(out var key))
            {
                var ss = key.Split("_");
                ProcessMinute(ss[0].ToInt(), ss[1].ToDateTime());
            }

            //// 统计1分钟之前数据
            //var time = DateTime.Now.AddMinutes(-1);

            //ProcessDay(time, appIds);
            //ProcessHour(time, appIds);
            //ProcessMinute(time, appIds);
        }

        private void ProcessDay(Int32 appId, DateTime time)
        {
            var date = time.Date;

            // 逐个应用计算
            var list = TraceData.SearchGroupAppAndName("day", date, new[] { appId });
            if (list.Count == 0) return;

            // 统计对象
            var sts = TraceDayStat.Search(date, new[] { appId });

            // 聚合
            foreach (var item in list)
            {
                if (item.Name.IsNullOrEmpty()) continue;

                item.StatDate = date;
                var st = TraceDayStat.FindOrAdd(sts, item);
                st.Total = item.Total;
                st.Errors = item.Errors;
                st.TotalCost = item.TotalCost;
                st.MaxCost = item.MaxCost;
                st.MinCost = item.MinCost;
            }

            // 保存统计
            sts.Update(true);
        }

        private void ProcessHour(Int32 appId, DateTime time)
        {
            time = time.Date.AddHours(time.Hour);

            // 逐个应用计算
            var list = TraceData.SearchGroupAppAndName("hour", time, new[] { appId });
            if (list.Count == 0) return;

            // 统计对象
            var sts = TraceHourStat.Search(time, new[] { appId });

            // 聚合
            foreach (var item in list)
            {
                if (item.Name.IsNullOrEmpty()) continue;

                item.StatHour = time;
                var st = TraceHourStat.FindOrAdd(sts, item);
                st.Total = item.Total;
                st.Errors = item.Errors;
                st.TotalCost = item.TotalCost;
                st.MaxCost = item.MaxCost;
                st.MinCost = item.MinCost;
            }

            // 保存统计
            sts.Update(true);
        }

        private void ProcessMinute(Int32 appId, DateTime time)
        {
            time = time.Date.AddHours(time.Hour).AddMinutes(time.Minute / 5 * 5);

            // 逐个应用计算
            // 统计数据
            var list = TraceData.SearchGroupAppAndName("minute", time, new[] { appId });
            if (list.Count == 0) return;

            // 统计对象
            var sts = TraceMinuteStat.Search(time, new[] { appId });

            // 聚合
            foreach (var item in list)
            {
                if (item.Name.IsNullOrEmpty()) continue;

                item.StatMinute = time;
                var st = TraceMinuteStat.FindOrAdd(sts, item);
                st.Total = item.Total;
                st.Errors = item.Errors;
                st.TotalCost = item.TotalCost;
                st.MaxCost = item.MaxCost;
                st.MinCost = item.MinCost;
            }

            // 保存统计
            sts.Update(true);
        }
    }
}