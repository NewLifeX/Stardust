#if NET5_0_OR_GREATER
using NewLife.Log;
using NewLife.Reflection;

namespace Stardust.Monitors;

/// <summary>Http诊断监听器</summary>
public class HttpDiagnosticListener : TraceDiagnosticListener
{
    private static HttpRequestOptionsKey<ISpan?> _spanKey = new("Star-Http-Span");

    /// <summary>实例化</summary>
    public HttpDiagnosticListener() => Name = "HttpHandlerDiagnosticListener";

    /// <summary>下一步</summary>
    /// <param name="value"></param>
    public override void OnNext(KeyValuePair<String, Object?> value)
    {
        if (Tracer == null) return;
#if DEBUG
        //XTrace.WriteLine("OnNext {0}", value.Key);
#endif

        switch (value.Key)
        {
            case "System.Net.Http.HttpRequestOut.Start":
                {
                    if (value.Value != null && value.Value.GetValue("Request") is HttpRequestMessage request &&
                        !request.Headers.Contains("traceparent"))
                    {
                        var span = Tracer.NewSpan(request);
                        request.Options.Set(_spanKey, span);
                    }

                    break;
                }
            case "System.Net.Http.Exception":
                {
                    if (value.Value != null && value.Value.GetValue("Request") is HttpRequestMessage request &&
                        request.Options.TryGetValue(_spanKey, out var span) && span != null)
                    {
                        if (value.Value.GetValue("Exception") is Exception ex)
                        {
                            span.SetError(ex, null);
                        }
                        span.Dispose();
                    }
                    break;
                }

            case "System.Net.Http.HttpRequestOut.Stop":
                {
                    if (value.Value != null && value.Value.GetValue("Request") is HttpRequestMessage request &&
                        request.Options.TryGetValue(_spanKey, out var span) && span != null)
                    {
                        if (value.Value.GetValue("Response") is HttpResponseMessage response)
                        {
                            //!! 这里不能使用响应内容作为Tag信息，因为响应内容还没有解压缩
                            ////todo NewLife.Core升级后使用以下这一行
                            //span.AppendTag(response);

                            //// 正常响应，部分作为Tag信息
                            //if (response.StatusCode == HttpStatusCode.OK)
                            //{
                            //    if (span is DefaultSpan ds && ds.TraceFlag > 0 && (span.Tag.IsNullOrEmpty() || span.Tag.Length < 1024))
                            //    {
                            //        // 判断类型和长度
                            //        var content = response.Content;
                            //        var mediaType = content.Headers?.ContentType?.MediaType;
                            //        var len = content.Headers?.ContentLength ?? 0;
                            //        if (len >= 0 && len < 1024 && mediaType.EndsWithIgnoreCase("json", "text"))
                            //        {
                            //            var result = content.ReadAsStringAsync().Result;
                            //            span.Tag = (span.Tag + "\r\n\r\n" + result).Cut(1024);
                            //        }
                            //    }
                            //}
                            //// 异常响应，记录错误
                            //else if (response.StatusCode > (HttpStatusCode)299)
                            //{
                            //    if (span.Error.IsNullOrEmpty()) span.Error = response.ReasonPhrase;
                            //}
                        }

                        span.Dispose();
                    }
                    break;
                }
        }
    }
}
#endif