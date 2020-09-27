using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using NewLife;
using NewLife.Model;
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
        /// <summary>开始时间</summary>
        public DateTime StartTime { get; set; }

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
                    var key = $"{item.AppId}_{item.StatMinute:yyyyMMdd}";
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
                var key = $"{appId}_{minute:yyyyMMdd}";
                var bag = _bagMinute.GetOrAdd(key, new ConcurrentBag<DateTime>());
                if (!bag.Contains(minute)) bag.Add(minute);
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

        /// <summary>流式计算，增量累加</summary>
        /// <param name="state"></param>
        private void DoFlowStat(Object state)
        {
            if (_queue.IsEmpty) return;

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
                    var key = $"{td.StatHour}#{td.AppId}#{td.Name}";
                    var st = _hourQueue.GetOrAdd(key, k => new TraceHourStat { StatTime = td.StatHour, AppId = td.AppId, Name = td.Name });

                    st.Total += td.Total;
                    st.Errors += td.Errors;
                    st.TotalCost += td.TotalCost;
                    if (st.MaxCost < td.MaxCost) st.MaxCost = td.MaxCost;
                    if (st.MinCost <= 0 || st.MinCost > td.MinCost && td.MinCost > 0) st.MinCost = td.MinCost;

                    _hourQueue.Commit(key);
                }

                // 分钟
                {
                    var key = $"{td.StatMinute}#{td.AppId}#{td.Name}";
                    var st = _minuteQueue.GetOrAdd(key, k => new TraceMinuteStat { StatTime = td.StatMinute, AppId = td.AppId, Name = td.Name });

                    st.Total += td.Total;
                    st.Errors += td.Errors;
                    st.TotalCost += td.TotalCost;
                    if (st.MaxCost < td.MaxCost) st.MaxCost = td.MaxCost;
                    if (st.MinCost <= 0 || st.MinCost > td.MinCost && td.MinCost > 0) st.MinCost = td.MinCost;

                    _minuteQueue.Commit(key);
                }
            }
        }

        /// <summary>批计算，覆盖缺失</summary>
        /// <param name="state"></param>
        private void DoBatchStat(Object state)
        {
            foreach (var item in _bagMinute)
            {
                var ss = item.Key.Split("_");
                var appId = ss[0].ToInt();
                var list = new List<DateTime>();
                while (item.Value.TryTake(out var dt))
                {
                    if (!list.Contains(dt)) list.Add(dt);
                }

                // 批量处理该应用，取最小时间和最大时间
                ProcessMinute(appId, list.Min(), list.Max());
            }

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

            // 统计数据
            //var list = TraceData.SearchGroupAppAndName("day", date, new[] { appId });
            var list = TraceMinuteStat.SearchGroup(appId, date, date.AddDays(1));
            if (list.Count == 0) return;

            // 聚合
            foreach (var item in list)
            {
                if (item.Name.IsNullOrEmpty()) continue;

                //var key = $"{date}#{appId}#{item.Name}";
                var st = _dayQueue.GetOrAdd(date, appId, item.Name, out var key);

                st.Total = item.Total;
                st.Errors = item.Errors;
                st.TotalCost = item.TotalCost;
                st.MaxCost = item.MaxCost;
                st.MinCost = item.MinCost;

                _dayQueue.Commit(key);
            }
        }

        private void ProcessHour(Int32 appId, DateTime time)
        {
            if (appId <= 0 || time.Year < 2000) return;

            time = time.Date.AddHours(time.Hour);

            // 统计数据
            //var list = TraceData.SearchGroupAppAndName("hour", time, new[] { appId });
            var list = TraceMinuteStat.SearchGroup(appId, time, time.AddHours(1));
            if (list.Count == 0) return;

            // 聚合
            foreach (var item in list)
            {
                if (item.Name.IsNullOrEmpty()) continue;

                //var key = $"{time}#{appId}#{item.Name}";
                var st = _hourQueue.GetOrAdd(time, appId, item.Name, out var key);

                st.Total = item.Total;
                st.Errors = item.Errors;
                st.TotalCost = item.TotalCost;
                st.MaxCost = item.MaxCost;
                st.MinCost = item.MinCost;

                _hourQueue.Commit(key);
            }
        }

        //private void ProcessMinute(Int32 appId, DateTime time)
        //{
        //    if (appId <= 0 || time.Year < 2000) return;

        //    time = time.Date.AddHours(time.Hour).AddMinutes(time.Minute / 5 * 5);

        //    // 统计数据
        //    var list = TraceData.SearchGroupAppAndName("minute", time, new[] { appId });
        //    if (list.Count == 0) return;

        //    // 统计对象
        //    var sts = TraceMinuteStat.Search(time, new[] { appId });

        //    // 聚合
        //    foreach (var item in list)
        //    {
        //        if (item.Name.IsNullOrEmpty()) continue;

        //        //var key = $"{time}#{appId}#{item.Name}";
        //        //var st = _minuteQueue.GetOrAdd(key, k => new TraceMinuteStat { StatTime = time, AppId = appId, Name = item.Name });

        //        item.StatMinute = time;
        //        var st = TraceMinuteStat.FindOrAdd(sts, item);

        //        st.Total = item.Total;
        //        st.Errors = item.Errors;
        //        st.TotalCost = item.TotalCost;
        //        st.MaxCost = item.MaxCost;
        //        st.MinCost = item.MinCost;

        //        //_minuteQueue.Commit(key);

        //        // 保存统计
        //        sts.Update(true);
        //    }
        //}

        private void ProcessMinute(Int32 appId, DateTime start, DateTime end)
        {
            if (appId <= 0 || start.Year < 2000 || end.Year < 2000) return;

            start = start.Date.AddHours(start.Hour).AddMinutes(start.Minute / 5 * 5);
            end = end.Date.AddHours(end.Hour).AddMinutes(end.Minute / 5 * 5);

            // 统计数据
            var list = TraceData.SearchGroupAppAndName(appId, start, end.AddMinutes(1));
            if (list.Count == 0) return;

            //// 统计对象
            //var sts = TraceMinuteStat.Search(appId, null, start, end.AddMinutes(1), null, null);

            // 聚合
            foreach (var item in list)
            {
                if (item.Name.IsNullOrEmpty()) continue;

                //var key = $"{time}#{appId}#{item.Name}";
                //var st = _minuteQueue.GetOrAdd(key, k => new TraceMinuteStat { StatTime = time, AppId = appId, Name = item.Name });

                var model = new TraceStatModel { Time = item.StatMinute, AppId = appId, Name = item.Name };
                var st = TraceMinuteStat.FindOrAdd(model);
                //var st = TraceMinuteStat.FindOrAdd(sts, item);

                st.Total = item.Total;
                st.Errors = item.Errors;
                st.TotalCost = item.TotalCost;
                st.MaxCost = item.MaxCost;
                st.MinCost = item.MinCost;

                //_minuteQueue.Commit(key);

                // 保存统计
                //sts.Update(true);
                st.Update();
            }
        }
    }

    class DayQueue : EntityDeferredQueue
    {
        public TraceDayStat GetOrAdd(DateTime date, Int32 appId, String name, out String key)
        {
            var model = new TraceStatModel { Time = date, AppId = appId, Name = name };
            key = model.Key;
            return GetOrAdd(key, k => TraceDayStat.FindOrAdd(model));
        }
    }

    class HourQueue : EntityDeferredQueue
    {
        public TraceHourStat GetOrAdd(DateTime date, Int32 appId, String name, out String key)
        {
            var model = new TraceStatModel { Time = date, AppId = appId, Name = name };
            key = model.Key;
            return GetOrAdd(key, k => TraceHourStat.FindOrAdd(model));
        }
    }

    class MinuteQueue : EntityDeferredQueue
    {
        public TraceMinuteStat GetOrAdd(DateTime date, Int32 appId, String name, out String key)
        {
            var model = new TraceStatModel { Time = date, AppId = appId, Name = name };
            key = model.Key;
            return GetOrAdd(key, k => TraceMinuteStat.FindOrAdd(model));
        }
    }
}