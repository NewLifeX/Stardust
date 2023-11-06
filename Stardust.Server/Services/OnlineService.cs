using NewLife;
using NewLife.Data;
using NewLife.Log;
using NewLife.Threading;
using Stardust.Data;
using Stardust.Data.Nodes;

namespace Stardust.Server.Services;

/// <summary>在线服务</summary>
public class OnlineService : IHostedService
{
    #region 属性
    private TimerX _timer;
    private TimerX _timer2;
    private readonly RegistryService _registryService;
    private readonly StarServerSetting _setting;
    private readonly ITracer _tracer;
    #endregion

    #region 构造
    public OnlineService(RegistryService registryService, StarServerSetting setting, ITracer tracer)
    {
        _registryService = registryService;
        _setting = setting;
        _tracer = tracer;
    }
    #endregion

    #region 方法
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _timer = new TimerX(CheckAppOnline, null, 5_000, 30_000) { Async = true };
        _timer2 = new TimerX(CheckHealth, null, 5_000, 60_000) { Async = true };

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _timer.TryDispose();
        _timer2.TryDispose();

        return Task.CompletedTask;
    }

    private void CheckAppOnline(Object state)
    {
        // 节点超时
        var sessionTimeout = _setting.SessionTimeout;
        if (sessionTimeout > 0)
        {
            using var span = _tracer?.NewSpan(nameof(CheckAppOnline));

            var rs2 = AppOnline.ClearExpire(TimeSpan.FromSeconds(sessionTimeout), TimeSpan.FromDays(2));
            if (rs2 != null)
            {
                foreach (var olt in rs2)
                {
                    var app = olt?.App;
                    if (app != null)
                    {
                        var msg = $"[{app}]登录于{olt.CreateTime}，最后活跃于{olt.UpdateTime}";
                        app.WriteHistory("超时下线", true, msg, olt.Version, olt.CreateIP, olt.Client);

                        CheckOffline(app, "超时下线");
                    }
                }
            }

            //var rs3 = ConfigOnline.ClearExpire(TimeSpan.FromDays(7));
        }

        // 注册中心
        {
            var rs = AppService.ClearExpire(TimeSpan.FromDays(2));
            var rs2 = AppConsume.ClearExpire(TimeSpan.FromSeconds(sessionTimeout));
        }
    }

    private async Task CheckHealth(Object state)
    {
        using var span = _tracer?.NewSpan(nameof(CheckHealth));

        var page = new PageParameter { PageSize = 1000 };
        while (true)
        {
            var list = AppService.Search(-1, -1, null, true, null, page);
            if (list.Count == 0) break;

            foreach (var svc in list)
            {
                if (!svc.Enable || svc.Address.IsNullOrEmpty()) continue;

                var service = svc.Service;
                var url = service?.HealthAddress;
                if (service == null || !service.HealthCheck || url.IsNullOrEmpty()) continue;

                await _registryService.HealthCheck(svc);
            }

            page.PageIndex++;
        }
    }

    public static void CheckOffline(App app, String reason)
    {
        // 下线告警
        if (app.AlarmOnOffline)
        {
            var webhook = RobotHelper.GetAlarm(app.Project, app.Category, app.WebHook);
            if (webhook.IsNullOrEmpty()) return;

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