#if NET5_0_OR_GREATER
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using NewLife;
using NewLife.Log;
using NewLife.Reflection;

namespace Stardust.Monitors
{
    /// <summary>Http诊断监听器</summary>
    public class HttpDiagnosticListener : TraceDiagnosticListener
    {
        private static HttpRequestOptionsKey<ISpan> _spanKey = new("Span");

        /// <summary>实例化</summary>
        public HttpDiagnosticListener() => Name = "HttpHandlerDiagnosticListener";

        /// <summary>下一步</summary>
        /// <param name="value"></param>
        public override void OnNext(KeyValuePair<String, Object> value)
        {
            switch (value.Key)
            {
                case "System.Net.Http.HttpRequestOut.Start":
                    {
                        if (value.Value.GetValue("Request") is HttpRequestMessage request && !request.Headers.Contains("traceparent"))
                        {
                            var span = Tracer.NewSpan(request);
                            request.Options.Set(_spanKey, span);
                        }

                        break;
                    }
                case "System.Net.Http.Exception":
                    {
                        if (value.Value.GetValue("Request") is HttpRequestMessage request && request.Options.TryGetValue(_spanKey, out var span))
                        {
                            if (value.Value.GetValue("Exception") is Exception ex)
                            {
                                span.SetError(ex, null);
                            }
                        }
                        break;
                    }

                case "System.Net.Http.HttpRequestOut.Stop":
                    {
                        if (value.Value.GetValue("Request") is HttpRequestMessage request && request.Options.TryGetValue(_spanKey, out var span))
                        {
                            if (value.Value.GetValue("Response") is HttpResponseMessage response && response.StatusCode > (HttpStatusCode)299)
                            {
                                if (span.Error.IsNullOrEmpty()) span.Error = response.ReasonPhrase;
                            }

                            span.Dispose();
                        }
                        break;
                    }
            }
        }
    }
}
#endif