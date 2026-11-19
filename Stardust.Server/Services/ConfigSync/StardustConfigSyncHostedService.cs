using NewLife;
using NewLife.Caching;
using NewLife.Log;
using NewLife.Messaging;
using XCode.Configuration;

namespace Stardust.Server.Services.ConfigSync;

/// <summary>集群配置同步后台服务</summary>
/// <remarks>
/// 启动后订阅事件总线，收到同一应用其它实例的变更通知时立即执行一次同步检查；
/// 本机保存配置后把变更广播出去，让其它实例尽快拉取最新配置。
/// 事件总线不可用时静默降级，仅依赖定时检查，不影响主流程。
/// </remarks>
public class StardustConfigSyncHostedService : IHostedService
{
    /// <summary>配置同步事件主题</summary>
    public const String Topic = "CubeConfigSync";

    private readonly ConfigSyncManager _manager;
    private readonly IServiceProvider _provider;
    private IEventBus<ConfigSyncEvent>? _bus;
    private String? _clientId;

    /// <summary>实例化</summary>
    /// <param name="manager">配置同步管理器</param>
    /// <param name="provider">服务提供者。用于解析事件总线工厂与缓存提供者</param>
    public StardustConfigSyncHostedService(ConfigSyncManager manager, IServiceProvider provider)
    {
        _manager = manager;
        _provider = provider;
    }

    /// <summary>启动服务。创建事件总线并订阅变更通知，然后启动定时同步</summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>任务</returns>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        // 节点标识同时作为订阅键，用于跳过事件总线回传的自身消息
        var clientId = Runtime.ClientId;

        // 星尘事件总线工厂优先（Redis队列/星尘通道），缺失时退回缓存提供者
        var bus = _provider.GetService<IEventBusFactory>()?.CreateEventBus<ConfigSyncEvent>(Topic, clientId);
        bus ??= _provider.GetService<ICacheProvider>()?.CreateEventBus<ConfigSyncEvent>(Topic, clientId);

        if (bus != null)
        {
            _bus = bus;
            _clientId = clientId;

            bus.Subscribe(new DelegateEventHandler<ConfigSyncEvent>(OnReceive), clientId);
            XTrace.WriteLine("ConfigSync[{0}] subscribed to {1} as {2}", _manager.AppId, Topic, clientId);
        }
        else
            XTrace.WriteLine("ConfigSync[{0}] event bus [{1}] is unavailable, rely on periodic check", _manager.AppId, Topic);

        _manager.Changed += OnChanged;
        _manager.Start();

        return Task.CompletedTask;
    }

    /// <summary>停止服务。停止定时同步并释放事件总线</summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>任务</returns>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _manager.Changed -= OnChanged;
        _manager.Stop();

        _bus.TryDispose();
        _bus = null;

        return Task.CompletedTask;
    }

    /// <summary>处理其它实例的配置变更通知</summary>
    /// <param name="e">配置同步事件</param>
    /// <returns>任务</returns>
    private Task OnReceive(ConfigSyncEvent e)
    {
        // 只处理本应用的事件，其它应用的配置由各自实例同步
        if (e != null && _manager.AppId.EqualIgnoreCase(e.AppId)) _manager.CheckNow(e.Scope);

        return Task.CompletedTask;
    }

    /// <summary>广播本机的配置变更通知</summary>
    /// <param name="sender">事件源</param>
    /// <param name="e">配置同步事件</param>
    private void OnChanged(Object? sender, ConfigSyncEvent e) => _ = PublishAsync(e);

    private async Task PublishAsync(ConfigSyncEvent e)
    {
        var bus = _bus;
        if (bus == null) return;

        try
        {
            // 携带本节点标识，订阅端据此跳过自身消息
            await bus.PublishAsync(e, new EventContext { Topic = Topic, ClientId = _clientId }).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            XTrace.WriteException(ex);
        }
    }
}
