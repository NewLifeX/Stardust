using NewLife;
using NewLife.Log;
using NewLife.Messaging;
using Stardust.Data;

namespace Stardust.Server.Services;

/// <summary>热门应用事件订阅服务。通过事件总线接收StarWeb广播的热门应用通知，替代内部HTTP接口调用</summary>
/// <remarks>
/// 星尘平台内部通信，直接使用 AppClient 的事件总线工厂（它实现 IEventBusFactory），固定走星尘通道。
/// 事件携带发布者标识（EventContext.ClientId），校验仅允许平台管理端应用（StarWeb/StarWebApi）广播，防止任意应用刷热窗口。
/// 延迟初始化等待星尘客户端就绪；总线不可用时静默降级，仅热窗口通知失效，不影响主流程。
/// </remarks>
/// <param name="starFactory">星尘工厂</param>
/// <param name="stat">追踪统计服务</param>
public class HotAppEventService(StarFactory starFactory, ITraceStatService stat) : IHostedService
{
    private IEventBus<Int32>? _bus;

    /// <summary>允许发布热窗口通知的平台管理端应用</summary>
    private static readonly String[] _allowedPublishers = ["StarWeb", "StarWebApi"];

    /// <summary>启动服务。延迟10秒初始化，避免与服务启动争抢资源，并等待星尘客户端就绪</summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>任务</returns>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _ = InitializeLaterAsync(cancellationToken);

        return Task.CompletedTask;
    }

    /// <summary>延迟初始化事件总线并订阅热门应用事件</summary>
    /// <param name="cancellationToken">取消令牌</param>
    private async Task InitializeLaterAsync(CancellationToken cancellationToken)
    {
        await Task.Delay(10_000, cancellationToken);

        // AppClient 本身就是事件总线工厂；Service 会触发星尘客户端惰性启动
        var bus = (starFactory.Service as AppClient)?.CreateEventBus<Int32>("Star-HotApp");
        if (bus != null)
        {
            bus.Subscribe(OnHotApp);
            _bus = bus;
            XTrace.Log.Info("Subscribed to hot application events");
        }
        else
            XTrace.Log.Warn("The hot application event bus is unavailable; hot-window notifications will be disabled");
    }

    /// <summary>停止服务，释放事件总线</summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>任务</returns>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        (_bus as IDisposable)?.Dispose();
        _bus = null;

        return Task.CompletedTask;
    }

    /// <summary>处理热门应用通知</summary>
    /// <param name="appId">应用编号</param>
    /// <param name="context">事件上下文。星尘通道携带发布者标识</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>任务</returns>
    private Task OnHotApp(Int32 appId, IEventContext? context, CancellationToken cancellationToken)
    {
        if (appId <= 0) return Task.CompletedTask;

        // 事件携带发布者标识时校验白名单；无标识时按内部信任处理
        var clientId = (context as EventContext)?.ClientId;
        if (!clientId.IsNullOrEmpty() && !IsAllowedPublisher(clientId))
        {
            XTrace.Log.Warn("Ignoring hot application notification from [{0}]: {1}", clientId, appId);
            return Task.CompletedTask;
        }

        stat.SetHotApp(appId);
        XTrace.Log.Debug("Received hot application notification: {0}, source: {1}", appId, clientId);

        return Task.CompletedTask;
    }

    /// <summary>是否允许的事件发布者。通过发布者标识反查在线应用，仅平台管理端应用（StarWeb/StarWebApi）可发布热窗口通知</summary>
    /// <param name="clientId">发布者客户端标识</param>
    /// <returns>是否允许</returns>
    public static Boolean IsAllowedPublisher(String? clientId)
    {
        if (clientId.IsNullOrEmpty()) return false;

        var app = AppOnline.FindByClient(clientId)?.App;
        return app != null && app.Name.EqualIgnoreCase(_allowedPublishers);
    }
}
