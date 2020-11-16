using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NewLife;
using NewLife.Caching;
using NewLife.Threading;
using Stardust.Data.Monitors;
using Stardust.DingTalk;
using Stardust.WeiXin;

namespace Stardust.Server.Services
{
    public interface IAlarmService
    {
        /// <summary>添加需要统计的应用，去重</summary>
        /// <param name="appId"></param>
        void Add(Int32 appId);
    }

    public class AlarmService : IAlarmService
    {
        /// <summary>计算周期。默认30秒</summary>
        public Int32 Period { get; set; } = 30;

        private readonly TimerX _timer;
        private readonly ConcurrentBag<Int32> _bag = new ConcurrentBag<Int32>();
        //private WeiXinClient _weixin;
        //private DingTalkClient _dingTalk;
        private readonly ICache _cache = new MemoryCache();

        public AlarmService() =>
            // 初始化定时器
            _timer = new TimerX(DoAlarm, null, 5_000, Period * 1000) { Async = true };

        /// <summary>添加需要统计的应用，去重</summary>
        /// <param name="appId"></param>
        public void Add(Int32 appId)
        {
            if (!_bag.Contains(appId)) _bag.Add(appId);
        }

        private void DoAlarm(Object state)
        {
            while (_bag.TryTake(out var appId))
            {
                //Process(appId);
            }

            var list = AppTracer.FindAllWithCache();
            foreach (var item in list)
            {
                Process(item.ID);
            }

            if (Period > 0) _timer.Period = Period * 1000;
        }

        private void Process(Int32 appId)
        {
            // 应用是否需要告警
            var app = AppTracer.FindByID(appId);
            if (app == null || !app.Enable || app.AlarmThreshold <= 0) return;
            if (app.AlarmRobot.IsNullOrEmpty()) return;

            // 最近一段时间的5分钟级数据
            var time = DateTime.Now;
            var minute = time.Date.AddHours(time.Hour).AddMinutes(time.Minute / 5 * 5);
            var st = AppMinuteStat.FindByAppIdAndTime(appId, minute);
            if (st == null) return;

            // 判断告警
            if (st.Errors >= app.AlarmThreshold)
            {
                // 一定时间内不要重复报错，除非错误翻倍
                var error2 = _cache.Get<Int32>("alarm:" + appId);
                if (error2 == 0 || st.Errors > error2 * 2)
                {
                    _cache.Set("alarm:" + appId, st.Errors, 5 * 60);

                    if (app.AlarmRobot.Contains("qyapi.weixin"))
                        SendWeixin(app, st);
                    else if (app.AlarmRobot.Contains("dingtalk"))
                        SendDingTalk(app, st);
                }
            }
        }

        private String GetMarkdown(AppTracer app, AppMinuteStat st, Boolean includeTitle)
        {
            var sb = new StringBuilder();
            if (includeTitle) sb.AppendLine($"### [{app}]系统告警");
            sb.AppendLine($">**总数：**<font color=\"info\">{st.Errors}</font>");

            // 找找具体接口错误
            var names = new List<String>();
            var sts = TraceMinuteStat.FindAllByAppIdAndTime(st.AppId, st.StatTime).OrderByDescending(e => e.StatTime).ToList();
            foreach (var item in sts)
            {
                if (item.Errors > 0)
                {
                    sb.AppendLine($">**错误：**<font color=\"info\">{item.StatTime.ToFullString()} 埋点[{item.Name}]共报错[{item.Errors:n0}]次</font>");

                    // 相同接口的错误，不要报多次
                    if (!names.Contains(item.Name))
                    {
                        var ds = TraceData.Search(st.AppId, item.Name, "minute", item.StatTime, 20);
                        if (ds.Count > 0)
                        {
                            var sms = SampleData.FindAllByDataIds(ds.Select(e => e.Id).ToArray()).Where(e => !e.Error.IsNullOrEmpty()).ToList();
                            if (sms.Count > 0)
                            {
                                sb.AppendLine($">**错误内容：**{sms[0].Error?.Trim()}");

                                names.Add(item.Name);
                            }
                        }
                    }
                }
            }

            var str = sb.ToString();
            if (str.Length > 2000) str = str.Substring(0, 2000);

            // 构造网址
            var url = Setting.Current.WebUrl;
            if (!url.IsNullOrEmpty())
            {
                url = url.EnsureEnd("/") + "Monitors/appMinuteStat?appId=" + st.AppId;
                str += Environment.NewLine + $"[更多信息]({url})";
            }

            return str;
        }

        private void SendWeixin(AppTracer app, AppMinuteStat st)
        {
            var _weixin = new WeiXinClient { Url = app.AlarmRobot };

            var msg = GetMarkdown(app, st, true);

            _weixin.SendMarkDown(msg);
        }

        private void SendDingTalk(AppTracer app, AppMinuteStat st)
        {
            var _dingTalk = new DingTalkClient { Url = app.AlarmRobot };

            var msg = GetMarkdown(app, st, false);

            _dingTalk.SendMarkDown("系统告警", msg, null);
        }
    }
}