using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using NewLife;
using NewLife.Log;
using NewLife.Reflection;

namespace Stardust.Monitors
{
    /// <summary>Http诊断监听器</summary>
    public class HttpDiagnosticListener : TraceDiagnosticListener
    {
        /// <summary>实例化</summary>
        public HttpDiagnosticListener()
        {
            Name = "HttpHandlerDiagnosticListener";
        }

        /// <summary>下一步</summary>
        /// <param name="value"></param>
        public override void OnNext(KeyValuePair<String, Object> value)
        {
            //base.OnNext(value);

#if DEBUG
            XTrace.WriteLine(value.Key);
#endif

            switch (value.Key)
            {
                case "System.Net.Http.HttpRequestOut.Start":
                    {
                        if (value.Value.GetValue("Request") is HttpRequestMessage request)
                        {
                            var current = Activity.Current;

                            Tracer.NewSpan(request);
                        }

                        break;
                    }
                case "System.Net.Http.Exception":
                    {
                        var span = DefaultSpan.Current;
                        if (span != null && value.Value.GetValue("Exception") is Exception ex)
                        {
                            span.SetError(ex, null);
                        }
                        break;
                    }

                case "System.Net.Http.HttpRequestOut.Stop":
                    {
                        var current = Activity.Current;

                        var span = DefaultSpan.Current;
                        if (span != null)
                        {
                            if (value.Value.GetValue("Response") is HttpResponseMessage response && !response.IsSuccessStatusCode)
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