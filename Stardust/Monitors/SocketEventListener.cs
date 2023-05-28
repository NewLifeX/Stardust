#if !NET40 && !NET45
using System.Diagnostics.Tracing;
using System.Net;
using NewLife;
using NewLife.Log;

namespace Stardust.Monitors;

/// <summary>Socket事件监听器</summary>
public class SocketEventListener : EventListener
{
    /// <summary>追踪器</summary>
    public ITracer Tracer { get; set; }

    /// <summary>创建事件源。此时决定要不要跟踪</summary>
    /// <param name="eventSource"></param>
    protected override void OnEventSourceCreated(EventSource eventSource)
    {
        if (eventSource.Name == "System.Net.Sockets")
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
        if (eventData.EventName == "ConnectStart")
        {
            if (eventData.Payload.Count > 0)
            {
                var host = eventData.Payload[0] + "";

                // SocketAddress 转 IPEndPoint
                var str = host.Substring("{", "}");
                if (!str.IsNullOrEmpty())
                {
                    try
                    {
                        // 从SocketAddress中解析出IP和端口
                        var ns = str.SplitAsInt(",");
                        var port = (ns[0] << 8) + ns[1];
                        var buf = ns.Select(e => (Byte)e).Skip(2).ToArray();
                        if (buf.Length >= 24)
                        {
                            var addr = new IPAddress(buf.Skip(4).Take(16).ToArray(), buf.Skip(4 + 16).Take(4).ToArray().ToUInt32());
                            if (addr.IsIPv4MappedToIPv6) addr = addr.MapToIPv4();
                            var ep = new IPEndPoint(addr, port);
                            host = ep.ToString();
                        }
                        else if (buf.Length >= 4)
                        {
                            var addr = new IPAddress(buf.Take(4).ToArray());
                            var ep = new IPEndPoint(addr, port);
                            host = ep.ToString();
                        }
                    }
                    catch { }
                }
                var span = Tracer?.NewSpan($"socket:{host}");
                Append(span, eventData);
            }
        }
        else if (eventData.EventName == "ConnectStop")
        {
            if (DefaultSpan.Current is DefaultSpan span && span.Builder != null && span.Builder.Name.StartsWith("socket:"))
            {
                Append(span, eventData);
                span.Dispose();
            }
        }
        else if (eventData.EventName == "ConnectFailed")
        {
            if (DefaultSpan.Current is DefaultSpan span && span.Builder != null && span.Builder.Name.StartsWith("socket:"))
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