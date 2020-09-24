using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using NewLife.Threading;
using Stardust.Data.Monitors;
using XCode;

namespace Stardust.Server.Services
{
    /// <summary>跟踪统计服务</summary>
    public interface ITraceStatService
    {
        /// <summary>添加需要统计的应用，去重</summary>
        /// <param name="appId"></param>
        void Add(Int32 appId);
    }

    /// <summary>跟踪统计服务</summary>
    public class TraceStatService : ITraceStatService
    {
        private TimerX _timer;
        private readonly ConcurrentBag<Int32> _bag = new ConcurrentBag<Int32>();

        /// <summary>添加需要统计的应用，去重</summary>
        /// <param name="appId"></param>
        public void Add(Int32 appId)
        {
            if (!_bag.Contains(appId)) _bag.Add(appId);

            // 初始化定时器
            if (_timer == null)
            {
                lock (this)
                {
                    if (_timer == null) _timer = new TimerX(DoTraceStat, null, 5_000, 30_000) { Async = true };
                }
            }
        }

        private void DoTraceStat(Object state)
        {
            // 拿到需要统计的应用
            var appIds = new List<Int32>();
            while (_bag.TryTake(out var id))
            {
                appIds.Add(id);
            }
            appIds = appIds.Distinct().ToList();
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
                    var st = sts.FirstOrDefault(e => e.AppId == item.AppId && e.Name == item.Name);
                    if (st == null)
                    {
                        st = new TraceDayStat { StatDate = date, AppId = item.AppId, Name = item.Name };
                        sts.Add(st);
                    }

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
                    var st = sts.FirstOrDefault(e => e.AppId == item.AppId && e.Name == item.Name);
                    if (st == null)
                    {
                        st = new TraceHourStat { StatTime = time, AppId = item.AppId, Name = item.Name };
                        sts.Add(st);
                    }

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
                    var st = sts.FirstOrDefault(e => e.AppId == item.AppId && e.Name == item.Name);
                    if (st == null)
                    {
                        st = new TraceMinuteStat { StatTime = time, AppId = item.AppId, Name = item.Name };
                        sts.Add(st);
                    }

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