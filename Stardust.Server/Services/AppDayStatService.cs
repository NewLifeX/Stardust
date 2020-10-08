using System;
using System.Collections.Concurrent;
using System.Linq;
using NewLife.Log;
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
            // 统计数据，每日跟踪根据应用和类型分组
            var list = TraceDayStat.SearchGroupApp(date);
            if (list.Count == 0) return;

            // 统计对象
            var sts = AppDayStat.Search(date, null);

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
                st.MinCost = ds.Min(e => e.MinCost);

                // 分类统计，应用有可能缺失某些类别
                st.Apis = ds.Where(e => e.Type == "api").Sum(e => e.Total);
                st.Https = ds.Where(e => e.Type == "http").Sum(e => e.Total);
                st.Dbs = ds.Where(e => e.Type == "db").Sum(e => e.Total);
                st.Mqs = ds.Where(e => e.Type == "mq").Sum(e => e.Total);
                st.Redis = ds.Where(e => e.Type == "redis").Sum(e => e.Total);
                st.Others = ds.Where(e => e.Type == "other").Sum(e => e.Total);
            }

            // 保存统计
            sts.Save(true);
        }
    }
}