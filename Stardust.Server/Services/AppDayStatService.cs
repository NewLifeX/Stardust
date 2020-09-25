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
            foreach (var item in list)
            {
                var st = sts.FirstOrDefault(e => e.AppId == item.AppId);
                if (st == null)
                {
                    st = new AppDayStat { StatDate = date, AppId = item.AppId };
                    sts.Add(st);
                }

                st.Total = item.Total;
                st.Errors = item.Errors;
                st.TotalCost = item.TotalCost;
                st.MaxCost = item.MaxCost;
                st.MinCost = item.MinCost;
            }

            // 保存统计
            sts.Save(true);
        }
    }
}