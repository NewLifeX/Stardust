#if !NET40 && !NET45
using System.Diagnostics.Tracing;
using NewLife.Log;

namespace Stardust.Monitors;

/// <summary>事件监听器基类</summary>
public abstract class EventListenerBase : EventListener
{
    /// <summary>事件源名称</summary>
    public String Name { get; set; }

    /// <summary>追踪器</summary>
    public ITracer? Tracer { get; set; }

    /// <summary>实例化</summary>
    /// <param name="name"></param>
    public EventListenerBase(String name) => Name = name;

    /// <summary>创建事件源。此时决定要不要跟踪</summary>
    /// <param name="eventSource"></param>
    protected override void OnEventSourceCreated(EventSource eventSource)
    {
        if (eventSource.Name == Name)
        {
            var log = XTrace.Log;

            var level = log.Level switch
            {
                LogLevel.All => EventLevel.LogAlways,
                LogLevel.Debug => EventLevel.Verbose,
                LogLevel.Info => EventLevel.Informational,
                LogLevel.Warn => EventLevel.Warning,
                LogLevel.Error => EventLevel.Error,
                LogLevel.Fatal => EventLevel.Critical,
                LogLevel.Off => throw new NotImplementedException(),
                _ => EventLevel.Informational,
            };

            EnableEvents(eventSource, level);
        }
    }

    ///// <summary>写入事件。监听器拦截，并写入日志</summary>
    ///// <param name="eventData"></param>
    //protected override void OnEventWritten(EventWrittenEventArgs eventData)
    //{
    //    base.OnEventWritten(eventData);
    //}

    /// <summary>事件数据附加到埋点中</summary>
    /// <param name="span"></param>
    /// <param name="eventData"></param>
    protected static void Append(ISpan span, EventWrittenEventArgs eventData)
    {
        if (span == null) return;
        if (eventData.PayloadNames == null || eventData.Payload == null) return;

        var dic = new Dictionary<String, Object?>();
        for (var i = 0; i < eventData.PayloadNames.Count && i < eventData.Payload.Count; i++)
        {
            dic[eventData.PayloadNames[i] + ""] = eventData.Payload[i];
        }
        if (dic.Count > 0) span?.AppendTag(dic);
    }
}
#endif