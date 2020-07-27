#if __CORE__
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using NewLife;
using NewLife.Log;
using NewLife.Reflection;

namespace Stardust.Monitors
{
    /// <summary>ASP.NETCore诊断监听器</summary>
    public class AspNetCoreDiagnosticListener : TraceDiagnosticListener
    {
        /// <summary>实例化</summary>
        public AspNetCoreDiagnosticListener()
        {
            Name = "Microsoft.AspNetCore";
        }

        /// <summary>下一步</summary>
        /// <param name="value"></param>
        public override void OnNext(KeyValuePair<String, Object> value)
        {
            switch (value.Key)
            {
                case "Microsoft.AspNetCore.Hosting.BeginRequest":
                    {
                        if (value.Value.GetValue("Request") is HttpRequestMessage request)
                        {
                            Tracer.NewSpan(request);
                        }

                        break;
                    }
                case "Microsoft.AspNetCore.Hosting.UnhandledException":
                    {
                        var span = DefaultSpan.Current;
                        if (span != null && value.Value.GetValue("Exception") is Exception ex)
                        {
                            span.SetError(ex, null);
                        }
                        break;
                    }

                case "Microsoft.AspNetCore.Hosting.EndRequest":
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

        private static String GetAction(HttpContext ctx)
        {
            var p = ctx.Request.Path + "";
            if (p.EndsWithIgnoreCase(ExcludeSuffixes)) return null;

            var ss = p.Split('/');
            if (ss.Length == 0) return p;

            // 如果最后一段是数字，则可能是参数，需要去掉
            if ((ss.Length == 4 || ss.Length == 5) && ss[ss.Length - 1].ToInt() > 0) p = "/" + ss.Take(ss.Length - 1).Join("/");

            return p;
        }
    }
}
#endif