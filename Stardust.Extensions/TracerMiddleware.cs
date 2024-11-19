﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using NewLife;
using NewLife.Collections;
using NewLife.Log;
using NewLife.Web;
using HttpContext = Microsoft.AspNetCore.Http.HttpContext;

namespace Stardust.Extensions;

/// <summary>性能跟踪中间件</summary>
public class TracerMiddleware
{
    private readonly RequestDelegate _next;

    /// <summary>跟踪器</summary>
    public static ITracer? Tracer { get; set; }

    /// <summary>实例化</summary>
    /// <param name="next"></param>
    public TracerMiddleware(RequestDelegate next) => _next = next ?? throw new ArgumentNullException(nameof(next));

    /// <summary>调用</summary>
    /// <param name="ctx"></param>
    /// <returns></returns>
    public async Task Invoke(HttpContext ctx)
    {
        //!! 以下代码不能封装为独立方法，因为有异步存在，代码被拆分为状态机，导致这里建立的埋点span无法关联页面接口内的下级埋点
        ISpan? span = null;
        var action = "";
        if (Tracer != null && !ctx.WebSockets.IsWebSocketRequest)
        {
            action = GetAction(ctx);
            if (!action.IsNullOrEmpty())
            {
                // 请求主体作为强制采样的数据标签，便于分析链路
                var req = ctx.Request;

                span = Tracer.NewSpan(action);
                span.Tag = $"{ctx.GetUserHost()} {req.Method} {req.GetRawUrl()}";
                span.Detach(req.Headers);
                span.Value = req.ContentLength ?? 0;
                if (span is DefaultSpan ds && ds.TraceFlag > 0)
                {
                    var flag = false;
                    if (req.ContentLength != null &&
                        req.ContentLength < 1024 &&
                        req.ContentType != null &&
                        req.ContentType.StartsWithIgnoreCase(TagTypes))
                    {
                        var buf = Pool.Shared.Rent(1024);
                        try
                        {
                            req.EnableBuffering();

                            var count = await req.Body.ReadAsync(buf, 0, buf.Length);
                            span.AppendTag("\r\n<=\r\n" + buf.ToStr(null, 0, count));
                            req.Body.Position = 0;
                            flag = true;
                        }
                        catch (Exception ex)
                        {
                            XTrace.Log.Error("[{0}]读取请求主体失败：{1}", action, ex.Message);
                        }
                        finally
                        {
                            Pool.Shared.Return(buf);
                        }
                    }

                    if (span.Tag.Length < 500)
                    {
                        if (!flag) span.AppendTag("\r\n<=");
                        var vs = req.Headers.Where(e => !e.Key.EqualIgnoreCase(ExcludeHeaders)).ToDictionary(e => e.Key, e => e.Value + "");
                        span.AppendTag("\r\n" + vs.Join(Environment.NewLine, e => $"{e.Key}:{e.Value}"));
                    }
                    else if (!flag)
                    {
                        span.AppendTag("\r\n<=\r\n");
                        span.AppendTag($"ContentLength: {req.ContentLength}\r\n");
                        span.AppendTag($"ContentType: {req.ContentType}");
                    }
                }
            }
        }

        try
        {
            await _next.Invoke(ctx);

            // 自动记录用户访问主机地址
            SaveServiceAddress(ctx);

            // 根据状态码识别异常
            if (span != null)
            {
                var res = ctx.Response;
                span.Value += res.ContentLength ?? 0;
                var code = res.StatusCode;
                if (code == 400 || code > 404)
                    span.SetError(new HttpRequestException($"Http Error {code} {(HttpStatusCode)code}"), null);
                else if (code == 200)
                {
                    if (span is DefaultSpan ds && ds.TraceFlag > 0 && (span.Tag == null || span.Tag.Length < 500))
                    {
                        var flag = false;
                        if (res.ContentLength != null &&
                            res.ContentLength < 1024 &&
                            res.Body.CanSeek &&
                            res.ContentType != null &&
                            res.ContentType.StartsWithIgnoreCase(TagTypes))
                        {
                            var buf = Pool.Shared.Rent(1024);
                            try
                            {
                                var p = res.Body.Position;
                                var count = await res.Body.ReadAsync(buf, 0, buf.Length);
                                span.AppendTag("\r\n=>\r\n" + buf.ToStr(null, 0, count));
                                res.Body.Position = p;
                                flag = true;
                            }
                            catch (Exception ex)
                            {
                                XTrace.Log.Error("[{0}]读取响应主体失败：{1}", action, ex.Message);
                            }
                            finally
                            {
                                Pool.Shared.Return(buf);
                            }
                        }

                        if (span.Tag == null || span.Tag.Length < 500)
                        {
                            if (!flag) span.AppendTag("\r\n=>");
                            var vs = res.Headers.Where(e => !e.Key.EqualIgnoreCase(ExcludeHeaders)).ToDictionary(e => e.Key, e => e.Value + "");
                            span.AppendTag("\r\n" + vs.Join(Environment.NewLine, e => $"{e.Key}:{e.Value}"));
                        }
                        else if (!flag)
                        {
                            span.AppendTag("\r\n=>\r\n");
                            span.AppendTag($"ContentLength: {res.ContentLength}\r\n");
                            span.AppendTag($"ContentType: {res.ContentType}");
                        }
                    }
                }
                else if (code == 404)
                {
                    // 取消404找不到路径的埋点，避免TraceItem过多
                    span?.Abandon();
                }
            }
        }
        catch (Exception ex)
        {
            //if (span != null)
            //{
            //    // 接口抛出ApiException时，认为是正常业务行为，埋点不算异常
            //    if (ex is ApiException)
            //        span.Tag ??= ex.Message;
            //    else
            //        span.SetError(ex, null);
            //}
            // 捕获所有未处理异常，即使是ApiException，也应该在接口层包装而不是继续向外抛出异常
            span?.SetError(ex, null);

            throw;
        }
        finally
        {
            span?.Dispose();
        }
    }

    /// <summary>支持作为标签数据的内容类型</summary>
    public static String[] TagTypes { get; set; } = [
        "text/plain", "text/xml", "application/json", "application/xml", "application/x-www-form-urlencoded"
    ];

    /// <summary>忽略的头部</summary>
    public static String[] ExcludeHeaders { get; set; } = ["traceparent", "Authorization", "Cookie"];

    /// <summary>忽略的后缀</summary>
    public static String[] ExcludeSuffixes { get; set; } = [
        ".html", ".htm", ".js", ".css", ".map", ".png", ".jpg", ".gif", ".ico",  // 脚本样式图片
        ".woff", ".woff2", ".svg", ".ttf", ".otf", ".eot"   // 字体
    ];
    private static readonly String[] CubeActions = ["index", "detail", "add", "edit", "delete", "deleteSelect", "deleteAll", "ExportCsv", "Info", "SetEnable", "EnableSelect", "DisableSelect", "DeleteSelect"];

    private static String? GetAction(HttpContext ctx)
    {
        var p = ctx.Request.Path + "";
        if (p.EndsWithIgnoreCase(ExcludeSuffixes)) return null;

        var ss = p.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (ss.Length == 0) return p;

        // 如果是魔方格式，保留3段，其它webapi接口只留2段
        if (ss.Length >= 3 && ss[2].EqualIgnoreCase(CubeActions))
            p = "/" + ss.Take(3).Join("/");
        else
            p = "/" + ss.Take(2).Join("/");

        return p;
    }

    /// <summary>自动记录用户访问主机地址</summary>
    /// <param name="ctx"></param>
    public static void SaveServiceAddress(HttpContext ctx)
    {
        var uri = ctx.Request.GetRawUrl();
        if (uri == null) return;

        // 排除本机地址
        var host = uri.Authority;
        var p = host.LastIndexOf(':');
        if (p >= 0) host = host[..p];
        if (host.EqualIgnoreCase("127.0.0.1", "localhost", "[::1]")) return;
        if (host.StartsWith("127.0.")) return;

        var baseAddress = $"{uri.Scheme}://{uri.Authority}";

        var set = NewLife.Setting.Current;
        var ss = set.ServiceAddress?.Split(",").ToList() ?? [];
        if (!ss.Contains(baseAddress))
        {
            // 过滤掉本机地址
            ss = ss.Where(e => !e.EqualIgnoreCase("127.0.0.1", "localhost", "[::1]") && !e.StartsWith("127.0.")).ToList();

            ss.Insert(0, baseAddress);
            set.ServiceAddress = ss.Take(5).Join(",");
            set.Save();
        }
    }
}