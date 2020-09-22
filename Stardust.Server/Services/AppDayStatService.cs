using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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
        /// <param name="appId"></param>
        void Add(Int32 appId);
    }

    /// <summary>应用统计服务</summary>
    public class AppDayStatService : IAppDayStatService
    {
        private TimerX _timer;
        private readonly ConcurrentBag<Int32> _bag = new ConcurrentBag<Int32>();

        /// <summary>添加需要统计的应用，去重</summary>
        /// <param name="appId"></param>
        public void Add(Int32 appId)
        {
            _bag.Add(appId);

            // 初始化定时器
            if (_timer == null)
            {
                lock (this)
                {
                    if (_timer == null) _timer = new TimerX(DoTraceStat, null, 5_000, 300_000) { Async = true };
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
            if (appIds.Count == 0) return;

            // 统计日期，凌晨0点10分之前统计前一天
            var time = DateTime.Now;
            if (time.Hour == 0 && time.Minute < 10) Process(time.AddDays(-1).Date, appIds);

            Process(time.Date, appIds);
        }

        private void Process(DateTime date, IList<Int32> appIds)
        {
            // 统计数据
            var list = TraceData.SearchGroupApp(date, appIds.ToArray());
            if (list.Count == 0) return;

            // 统计对象
            var sts = AppDayStat.Search(date, list.Select(e => e.AppId).ToArray());

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