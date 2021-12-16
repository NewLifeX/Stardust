using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using NewLife;
using NewLife.Log;
using NewLife.Threading;
using Stardust.Data;
using Stardust.Data.Configs;

namespace Stardust.Server.Services
{
    /// <summary>在线服务</summary>
    public class OnlineService : IHostedService
    {
        #region 属性
        private TimerX _timer;
        private readonly ITracer _tracer;
        #endregion

        #region 构造
        public OnlineService(ITracer tracer) => _tracer = tracer;
        #endregion

        #region 方法
        public Task StartAsync(CancellationToken cancellationToken)
        {
            _timer = new TimerX(CheckOnline, null, 5_000, 30_000) { Async = true };

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _timer.TryDispose();

            return Task.CompletedTask;
        }

        private void CheckOnline(Object state)
        {
            // 节点超时
            var set = Setting.Current;
            var sessionTimeout = set.SessionTimeout;
            if (sessionTimeout > 0)
            {
                using var span = _tracer?.NewSpan(nameof(CheckOnline));

                var rs2 = AppOnline.ClearExpire(TimeSpan.FromSeconds(sessionTimeout));
                if (rs2 != null)
                {
                    foreach (var olt in rs2)
                    {
                        var app = olt?.App;
                        var msg = $"[{app}]登录于{olt.CreateTime}，最后活跃于{olt.UpdateTime}";
                        var history = AppHistory.Create(app, "超时下线", true, msg, Environment.MachineName, olt.CreateIP);
                        history.Client = olt.Client;
                        history.Version = olt.Version;
                        history.SaveAsync();

                        if (app != null) CheckOffline(app, "超时下线");
                    }
                }

                var rs3 = ConfigOnline.ClearExpire(TimeSpan.FromSeconds(sessionTimeout));
            }

            // 注册中心
            {
                var rs = AppService.ClearExpire(TimeSpan.FromDays(7));
                var rs2 = AppConsume.ClearExpire(TimeSpan.FromSeconds(sessionTimeout));
            }
        }

        public static void CheckOffline(App app, String reason)
        {
            // 下线告警
            if (app.AlarmOnOffline && RobotHelper.CanAlarm(app.Category, app.WebHook))
            {
                // 查找该节点还有没有其它实例在线
                var olts = AppOnline.FindAllByApp(app.Id);
                if (olts.Count == 0)
                {
                    var msg = $"应用[{app.Name}]（{app.DisplayName}）已下线！{reason} IP={app.LastIP}";
                    RobotHelper.SendAlarm(app.Category, app.WebHook, "应用下线告警", msg);
                }
            }
        }
        #endregion
    }
}