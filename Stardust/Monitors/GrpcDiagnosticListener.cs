#if NET5_0_OR_GREATER
using NewLife.Log;
using NewLife.Reflection;

namespace Stardust.Monitors;

/// <summary>Grpc诊断监听器</summary>
public class GrpcDiagnosticListener : TraceDiagnosticListener
{
    private static HttpRequestOptionsKey<ISpan> _spanKey = new("Star-Grpc-Span");

    /// <summary>实例化</summary>
    public GrpcDiagnosticListener() => Name = "Grpc.Net.Client";

    /// <summary>下一步</summary>
    /// <param name="value"></param>
    public override void OnNext(KeyValuePair<String, Object> value)
    {
        if (Tracer == null) return;

        // 前缀可能是 Grpc.Net.Client.GrpcOut.
        var name = value.Key.Split(".").LastOrDefault();

        var span = DefaultSpan.Current;
        var spanName = (span as DefaultSpan)?.Builder?.Name;

        switch (name)
        {
            case "Start":
                if (value.Value.GetValue("Request") is HttpRequestMessage request &&
                    !request.Headers.Contains("traceparent"))
                {
                    var span2 = Tracer.NewSpan(request);
                    request.Options.Set(_spanKey, span2);
                }

                break;

            case "Stop":
                if (spanName.StartsWith("grpc:"))
                {
                    span.Dispose();
                }

                break;

            case "Error":
                if (spanName.StartsWith("grpc:"))
                {
                    if (value.Value.GetValue("Exception") is Exception ex) span.SetError(ex, null);

                    span.Dispose();
                }
                break;
        }
    }
}
#endif