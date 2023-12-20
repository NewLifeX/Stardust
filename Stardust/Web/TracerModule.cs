#if NET40_OR_GREATER
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using NewLife;
using NewLife.Log;

namespace Stardust.Web;

/// <summary>跟踪器模块</summary>
public class TracerModule : IHttpModule
{
    /// <summary>跟踪器</summary>
    public static ITracer? Tracer { get; set; } = DefaultTracer.Instance;

    void IHttpModule.Dispose() { }

    /// <summary>初始化模块，准备拦截请求。</summary>
    /// <param name="context"></param>
    void IHttpModule.Init(HttpApplication context)
    {
        context.BeginRequest += OnInit;
        context.PostReleaseRequestState += OnEnd;
    }

    void OnInit(Object sender, EventArgs e)
    {
        var app = sender as HttpApplication;
        var ctx = app?.Context;

        if (ctx != null && Tracer != null)
        {
#if NET45_OR_GREATER
            if (ctx.IsWebSocketRequest) return;
#endif
            var action = GetAction(ctx);
            if (!action.IsNullOrEmpty())
            {
                var span = Tracer.NewSpan(action);
                ctx.Items["__span"] = span;

                // 聚合请求头作为强制采样的数据标签
                var req = ctx.Request;
                var vs = new Dictionary<String, String>(StringComparer.OrdinalIgnoreCase);
                foreach (var item in req.Headers.AllKeys)
                {
                    vs[item] = req.Headers[item];
                }
                span.Tag = $"{req.UserHostAddress} {req.HttpMethod} {req.RawUrl}";
                if (span is DefaultSpan ds && ds.TraceFlag > 0)
                    span.Tag += Environment.NewLine + vs.Join(Environment.NewLine, e => $"{e.Key}:{e.Value}");
                span.Detach(vs);
            }
        }
    }

    void OnEnd(Object sender, EventArgs e)
    {
        var app = sender as HttpApplication;
        var ctx = app?.Context;
        if (ctx != null && ctx.Items["__span"] is ISpan span)
        {
            var ex = ctx.Error;
            if (ex != null) span.SetError(ex, null);

            span.Dispose();
        }
    }

    /// <summary>忽略的后缀</summary>
    public static String[] ExcludeSuffixes { get; set; } = new[] {
        ".html", ".htm", ".js", ".css", ".map", ".png", ".jpg", ".gif", ".ico",  // 脚本样式图片
        ".woff", ".woff2", ".svg", ".ttf", ".otf", ".eot"   // 字体
    };

    private static String? GetAction(HttpContext ctx)
    {
        var p = ctx.Request.Path + "";
        if (p.EndsWithIgnoreCase(ExcludeSuffixes)) return null;

        var ss = p.Split('/');
        if (ss.Length == 0) return p;

        // 如果是魔方格式，保留3段
        if (ss.Length >= 4 && ss[3].EqualIgnoreCase("detail", "add", "edit")) p = "/" + ss.Take(4).Join("/");

        return p;
    }
}
#endif