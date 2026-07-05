#if NET5_0_OR_GREATER
using System;
using System.Collections.Generic;
using NewLife;
using NewLife.Log;
using NewLife.Reflection;

namespace Stardust.Monitors;

/// <summary>ASP.NETCore诊断监听器</summary>
/// <remarks>
/// 通过反射访问 HttpContext，避免直接依赖 Microsoft.AspNetCore.Http 程序集。
/// payload 中 HttpContext 的 Request.Path、Response.StatusCode 等属性通过 GetValue 扩展方法获取。
/// </remarks>
public class AspNetCoreDiagnosticListener : TraceDiagnosticListener
{
    /// <summary>实例化</summary>
    public AspNetCoreDiagnosticListener()
    {
        Name = "Microsoft.AspNetCore";
    }

    /// <summary>下一步</summary>
    /// <param name="value"></param>
    public override void OnNext(KeyValuePair<String, Object?> value)
    {
        if (Tracer == null) return;

        switch (value.Key)
        {
            case "Microsoft.AspNetCore.Hosting.HttpRequestIn.Start":
                {
                    if (value.Value != null && value.Value.GetValue("HttpContext") is Object ctx)
                    {
                        var path = GetAction(ctx);
                        if (path == null) return;

                        Tracer.NewSpan(path);
                    }

                    break;
                }
            case "Microsoft.AspNetCore.Hosting.UnhandledException":
                {
                    var span = DefaultSpan.Current;
                    if (span != null && value.Value != null && value.Value.GetValue("Exception") is Exception ex)
                    {
                        span.SetError(ex, null);
                    }
                    break;
                }

            case "Microsoft.AspNetCore.Hosting.HttpRequestIn.Stop":
                {
                    var span = DefaultSpan.Current;
                    if (span != null)
                    {
                        span.Dispose();
                    }
                    break;
                }
        }
    }

    /// <summary>忽略的后缀</summary>
    public static String[] ExcludeSuffixes { get; set; } = new[] {
        ".html", ".htm", ".js", ".css", ".map", ".png", ".jpg", ".gif", ".ico",  // 脚本样式图片
        ".woff", ".woff2", ".svg", ".ttf", ".otf", ".eot"   // 字体
    };

    private static String? GetAction(Object ctx)
    {
        // 通过反射获取 ctx.Request.Path
        var request = ctx.GetValue("Request");
        if (request == null) return null;

        var path = request.GetValue("Path") + "";
        if (path.IsNullOrEmpty()) return null;

        if (path.EndsWithIgnoreCase(ExcludeSuffixes)) return null;

        var ss = path.Split('/');
        if (ss.Length == 0) return path;

        // 如果最后一段是数字，则可能是参数，需要去掉
        if ((ss.Length == 4 || ss.Length == 5) && ss[ss.Length - 1].ToInt() > 0) path = "/" + ss.Take(ss.Length - 1).Join("/");

        return path;
    }
}
#endif