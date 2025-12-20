using NewLife;
using NewLife.Log;
using NewLife.Messaging;
using NewLife.Serialization;

namespace Stardust.Services;

/// <summary>字符串事件上下文</summary>
public class StringEventContext<TEvent>(IEventBus<TEvent> eventBus, String message) : IEventContext<TEvent>
{
    /// <summary>事件总线</summary>
    public IEventBus<TEvent> EventBus { get; set; } = eventBus;

    /// <summary>原始消息</summary>
    public String Message { get; set; } = message;
}

/// <summary>星尘事件总线。借助星尘websocket链路收发消息</summary>
/// <typeparam name="TEvent"></typeparam>
/// <param name="client"></param>
/// <param name="topic"></param>
public class StarEventBus<TEvent>(AppClient client, String topic) : EventBus<TEvent>, IEventDispatcher<String> where TEvent : class
{
    #region 属性
    /// <summary>超时时间。默认5000毫秒</summary>
    public Int32 Timeout { get; set; } = 5_000;

    /// <summary>链路追踪</summary>
    public ITracer? Tracer { get; set; }
    #endregion

    #region 方法
    /// <summary>订阅消息。先本地再远程</summary>
    /// <param name="handler">事件处理器</param>
    /// <param name="clientId">客户端标识</param>
    /// <returns></returns>
    public override Boolean Subscribe(IEventHandler<TEvent> handler, String clientId = "")
    {
        // 先本地再远程
        if (!base.Subscribe(handler, clientId)) return false;

        try
        {
            client.PublishEventAsync(topic, "subscribe").Wait(Timeout);

            return true;
        }
        catch
        {
            client.PublishEventAsync(topic, "unsubscribe").Wait(Timeout);

            throw;
        }
    }

    /// <summary>取消订阅消息。先远程再本地</summary>
    /// <param name="clientId">客户端标识</param>
    /// <returns></returns>
    public override Boolean Unsubscribe(String clientId = "")
    {
        // 先远程再本地
        client.PublishEventAsync(topic, "unsubscribe").Wait(Timeout);

        return base.Unsubscribe(clientId);
    }

    /// <summary>发布消息到消息队列。经星尘平台转发给各应用</summary>
    /// <param name="event">事件</param>
    /// <param name="context">上下文</param>
    /// <param name="cancellationToken">取消令牌</param>
    public override async Task<Int32> PublishAsync(TEvent @event, IEventContext<TEvent>? context = null, CancellationToken cancellationToken = default)
    {
        // 待发布消息增加追踪标识
        if (@event is ITraceMessage tm && tm.TraceId.IsNullOrEmpty()) tm.TraceId = DefaultSpan.Current?.ToString();

        var json = client.JsonHost.Write(@event);
        await client.PublishEventAsync(topic, json, cancellationToken).ConfigureAwait(false);

        return json.Length;
    }

    /// <summary>分发处理消息。反序列化为事件对象后分发给本地事件处理器</summary>
    /// <param name="message">事件消息原文。一般是json形式</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public Task<Int32> DispatchAsync(String message, CancellationToken cancellationToken)
    {
        using var span = Tracer?.NewSpan($"event:{topic}", message);
        try
        {
            var context = new StringEventContext<TEvent>(this, message);
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

        return Task.FromResult(0);
    }
    #endregion
}
