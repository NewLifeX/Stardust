#if !NET40 && !NET45
using System.Diagnostics.Tracing;
using System.Net;
using NewLife;
using NewLife.Log;

namespace Stardust.Monitors;

/// <summary>Socket事件监听器。用于监听HttpClient下层连接</summary>
public class SocketEventListener : EventListenerBase
{
    /// <summary>实例化</summary>
    public SocketEventListener() : base("System.Net.Sockets") { }

    /// <summary>写入事件。监听器拦截，并写入日志</summary>
    /// <param name="eventData"></param>
    protected override void OnEventWritten(EventWrittenEventArgs eventData)
    {
#if DEBUG
        //XTrace.WriteLine(eventData.EventName);
#endif
        if (eventData.EventName == "ConnectStart")
        {
            if (eventData.Payload != null && eventData.Payload.Count > 0)
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
                if (span != null) Append(span, eventData);
            }
        }
        else if (eventData.EventName == "ConnectStop")
        {
            var span = DefaultSpan.Current;
            if (span != null && span.Name.StartsWith("socket:"))
            {
                Append(span, eventData);
                span.Dispose();
            }
        }
        else if (eventData.EventName == "ConnectFailed")
        {
            var span = DefaultSpan.Current;
            if (span != null && span.Name.StartsWith("socket:"))
            {
                Append(span, eventData);
                span.Error = eventData.EventName;
                span.Dispose();
            }
        }
    }
}
#endif