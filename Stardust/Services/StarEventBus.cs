using NewLife;
using NewLife.Log;
using NewLife.Messaging;
using NewLife.Serialization;
using NewLife.Threading;
#if !NET45
using TaskEx = System.Threading.Tasks.Task;
#endif

namespace Stardust.Services;

/// <summary>字符串事件上下文</summary>
public class StringEventContext(IEventBus eventBus, String message) : IEventContext
{
    /// <summary>事件总线</summary>
    public IEventBus EventBus { get; set; } = eventBus;

    /// <summary>原始消息</summary>
    public String Message { get; set; } = message;
}

/// <summary>星尘事件总线。借助星尘websocket链路收发消息</summary>
/// <typeparam name="TEvent"></typeparam>
/// <param name="client"></param>
/// <param name="topic"></param>
public class StarEventBus<TEvent>(AppClient client, String topic) : EventBus<TEvent>, IEventHandler<String>, ITracerFeature
{
    #region 属性
    /// <summary>超时时间。默认5000毫秒</summary>
    public Int32 Timeout { get; set; } = 5_000;

    /// <summary>是否准备好。客户端已登录且已订阅</summary>
    public Boolean IsReady => client.Logined && _subscribed;

    /// <summary>链路追踪</summary>
    public ITracer? Tracer { get; set; }

    private volatile Boolean _subscribed;
    private TimerX? _timer;
    #endregion

    #region 方法
    /// <summary>订阅消息。先本地再远程</summary>
    /// <param name="handler">事件处理器</param>
    /// <param name="clientId">客户端标识</param>
    /// <returns></returns>
    public override Boolean Subscribe(IEventHandler<TEvent> handler, String clientId = "") => SubscribeAsync(handler, clientId).GetAwaiter().GetResult();

    /// <summary>订阅消息。先本地再远程</summary>
    /// <param name="handler">事件处理器</param>
    /// <param name="clientId">客户端标识</param>
    /// <param name="cancellationToken">取消通知</param>
    /// <returns></returns>
    public override async Task<Boolean> SubscribeAsync(IEventHandler<TEvent> handler, String clientId = "", CancellationToken cancellationToken = default)
    {
        // 先本地再远程
        if (!base.Subscribe(handler, clientId)) return false;

        // 如果客户端没有准备好，则启动定时器延迟订阅，或者等发布消息的时候再订阅
        if (!client.Logined)
        {
            _timer = new TimerX(DoSubscribe, null, 5_000, 5_000) { Async = true };
            return true;
        }

        try
        {
            await RemoteSubscribe().ConfigureAwait(false);

            return true;
        }
        catch
        {
            base.Unsubscribe(clientId);

            throw;
        }
    }

    private async Task<Boolean> RemoteSubscribe()
    {
        if (!client.Logined) return false;
        if (_subscribed) return true;

        using var span = Tracer?.NewSpan($"event:{topic}:subscribe", topic);

        await client.PublishEventAsync(topic, "subscribe").ConfigureAwait(false);
        _subscribed = true;

        Log?.Info("事件总线[{0}]远程订阅成功！", topic);

        return true;
    }

    private async Task DoSubscribe(Object state)
    {
        // 在定时器里面订阅，等待客户端准备好
        if (!_subscribed)
        {
            try
            {
                var rs = await RemoteSubscribe().ConfigureAwait(false);
                if (!rs) return;
            }
            catch
            {
                return;
            }
        }

        _timer.TryDispose();
        _timer = null;
    }

    /// <summary>取消订阅消息。先远程再本地</summary>
    /// <param name="clientId">客户端标识</param>
    /// <returns></returns>
    public override Boolean Unsubscribe(String clientId = "") => UnsubscribeAsync(clientId).GetAwaiter().GetResult();

    /// <summary>取消订阅消息。先远程再本地</summary>
    /// <param name="clientId">客户端标识</param>
    /// <param name="cancellationToken">取消通知</param>
    /// <returns></returns>
    public override async Task<Boolean> UnsubscribeAsync(String clientId = "", CancellationToken cancellationToken = default)
    {
        using var span = Tracer?.NewSpan($"event:{topic}:unsubscribe", topic);

        // 先远程再本地
        await client.PublishEventAsync(topic, "unsubscribe", cancellationToken).ConfigureAwait(false);
        _subscribed = false;

        Log?.Info("事件总线[{0}]远程取消订阅成功！", topic);

        return base.Unsubscribe(clientId);
    }

    /// <summary>发布消息到消息队列。经星尘平台转发给各应用</summary>
    /// <param name="event">事件</param>
    /// <param name="context">上下文</param>
    /// <param name="cancellationToken">取消令牌</param>
    public override async Task<Int32> PublishAsync(TEvent @event, IEventContext? context = null, CancellationToken cancellationToken = default)
    {
        if (!client.Logined) return -1;
        if (@event == null) return 0;
        if (!_subscribed) await RemoteSubscribe().ConfigureAwait(false);

        // 待发布消息增加追踪标识
        if (@event is ITraceMessage tm && tm.TraceId.IsNullOrEmpty()) tm.TraceId = DefaultSpan.Current?.ToString();

        var json = client.JsonHost.Write(@event);
        await client.PublishEventAsync(topic, json, cancellationToken).ConfigureAwait(false);

        return json.Length;
    }

    /// <summary>分发处理消息。反序列化为事件对象后分发给本地事件处理器</summary>
    /// <param name="message">事件消息原文。一般是json形式</param>
    /// <param name="context">事件上下文。用于在发布者、订阅者及中间处理器之间传递协调数据，如 Handler、ClientId 等</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public Task HandleAsync(String message, IEventContext? context, CancellationToken cancellationToken)
    {
        using var span = Tracer?.NewSpan($"event:{topic}", message);
        try
        {
            context ??= new StringEventContext(this, message);
            if (message is not TEvent @event)
            {
                @event = client.JsonHost.Read<TEvent>(message)!;
                if (span != null && @event is ITraceMessage tm) span.Detach(tm.TraceId);
            }

            // 分发给本地处理器。必须调用基类方法
            return base.PublishAsync(@event!, context, cancellationToken);
        }
        catch (Exception ex)
        {
            span?.SetError(ex);
        }

        return TaskEx.CompletedTask;
    }
    #endregion
}
