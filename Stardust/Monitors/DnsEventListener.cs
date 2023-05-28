#if !NET40 && !NET45
using System;
using System.Diagnostics.Tracing;
using NewLife.Log;

namespace Stardust.Monitors;

/// <summary>DNS事件监听器</summary>
public class DnsEventListener : EventListener
{
    /// <summary>追踪器</summary>
    public ITracer Tracer { get; set; }

    /// <summary>创建事件源。此时决定要不要跟踪</summary>
    /// <param name="eventSource"></param>
    protected override void OnEventSourceCreated(EventSource eventSource)
    {
        if (eventSource.Name == "System.Net.NameResolution")
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

    /// <summary>写入事件。监听器拦截，并写入日志</summary>
    /// <param name="eventData"></param>
    protected override void OnEventWritten(EventWrittenEventArgs eventData)
    {
#if DEBUG
        //XTrace.WriteLine(eventData.EventName);
#endif
        if (eventData.EventName == "ResolutionStart")
        //if (eventData.Opcode == EventOpcode.Start)
        {
            if (eventData.Payload.Count > 0)
            {
                var host = eventData.Payload[0] + "";
                var span = Tracer?.NewSpan($"dns:{host}");
                Append(span, eventData);
            }
        }
        else if (eventData.EventName == "ResolutionStop")
        //else if (eventData.Opcode == EventOpcode.Stop)
        {
            if (DefaultSpan.Current is DefaultSpan span && span.Builder != null && span.Builder.Name.StartsWith("dns:"))
            {
                Append(span, eventData);
                span.Dispose();
            }
        }
        else if (eventData.EventName == "ResolutionFailed")
        {
            if (DefaultSpan.Current is DefaultSpan span && span.Builder != null && span.Builder.Name.StartsWith("dns:"))
            {
                Append(span, eventData);
                span.Error = eventData.EventName;
                span.Dispose();
            }
        }
    }

    static void Append(ISpan span, EventWrittenEventArgs eventData)
    {
        if (span == null) return;

        var dic = new Dictionary<String, Object>();
        for (var i = 0; i < eventData.PayloadNames.Count && i < eventData.Payload.Count; i++)
        {
            dic[eventData.PayloadNames[i] + ""] = eventData.Payload[i];
        }
        if (dic.Count > 0) span?.AppendTag(dic);
    }
}
#endif