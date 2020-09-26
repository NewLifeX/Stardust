using System;
using System.Collections.Concurrent;
using System.Linq;
using NewLife.Threading;
using Stardust.Data.Monitors;
using XCode;

namespace Stardust.Server.Services
{
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
        private TimerX _timer;
        private readonly ConcurrentBag<DateTime> _bag = new ConcurrentBag<DateTime>();

        /// <summary>添加需要统计的应用，去重</summary>
        /// <param name="date"></param>
        public void Add(DateTime date)
        {
            if (!_bag.Contains(date)) _bag.Add(date);

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
            while (_bag.TryTake(out var time))
            {
                Process(time.Date);
            }
        }

        private void Process(DateTime date)
        {
            // 统计数据
            var list = TraceDayStat.SearchGroupApp(date);
            if (list.Count == 0) return;

            // 统计对象
            var sts = AppDayStat.Search(date, null);

            // 聚合
            var dic = list.GroupBy(e => e.AppId);
            foreach (var item in dic)
            {
                var appId = item.Key;
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
                st.MinCost = ds.Min(e => e.MinCost);

                st.Apis = ds.FirstOrDefault(e => e.Type == "api")?.Total ?? 0;
                st.Https = ds.FirstOrDefault(e => e.Type == "http")?.Total ?? 0;
                st.Dbs = ds.FirstOrDefault(e => e.Type == "db")?.Total ?? 0;
                st.Mqs = ds.FirstOrDefault(e => e.Type == "mq")?.Total ?? 0;
                st.Redis = ds.FirstOrDefault(e => e.Type == "redis")?.Total ?? 0;
                st.Others = ds.FirstOrDefault(e => e.Type == "other")?.Total ?? 0;
            }

            // 保存统计
            sts.Save(true);
        }
    }
}