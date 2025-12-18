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
public class StarEventBus<TEvent>(AppClient client, String topic) : EventBus<TEvent> where TEvent : class
{
    #region 属性
    /// <summary>链路追踪</summary>
    public ITracer? Tracer { get; set; }
    #endregion

    #region 方法
    /// <summary>发布消息到消息队列</summary>
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

    /// <summary>处理消息</summary>
    /// <param name="message"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task Process(String message, CancellationToken cancellationToken)
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

            await base.PublishAsync(@event!, context, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            span?.SetError(ex);
        }
    }
    #endregion
}
