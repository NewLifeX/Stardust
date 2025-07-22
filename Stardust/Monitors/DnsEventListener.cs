#if !NET40 && !NET45
using System;
using System.Diagnostics.Tracing;
using NewLife;
using NewLife.Log;

namespace Stardust.Monitors;

/// <summary>DNS事件监听器</summary>
public class DnsEventListener : EventListenerBase
{
    /// <summary>实例化</summary>
    public DnsEventListener() : base("System.Net.NameResolution") { }

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
            if (eventData.Payload != null && eventData.Payload.Count > 0)
            {
                var host = eventData.Payload[0] + "";
                var ip = NetHelper.MyIP();
                var span = Tracer?.NewSpan($"dns:{host}:from:{ip}");
                if (span != null) Append(span, eventData);
            }
        }
        else if (eventData.EventName == "ResolutionStop")
        //else if (eventData.Opcode == EventOpcode.Stop)
        {
            var span = DefaultSpan.Current;
            if (span != null && span.Name.StartsWith("dns:"))
            {
                Append(span, eventData);
                span.Dispose();
            }
        }
        else if (eventData.EventName == "ResolutionFailed")
        {
            var span = DefaultSpan.Current;
            if (span != null && span.Name.StartsWith("dns:"))
            {
                Append(span, eventData);
                span.Error = eventData.EventName;
                span.Dispose();
            }
        }
    }
}
#endif