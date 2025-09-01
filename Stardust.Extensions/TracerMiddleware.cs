using System;
using System.Collections.Concurrent;
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
using Stardust.Monitors;
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
        var resolver = Tracer?.Resolver as StarTracerResolver;
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
                    var size = resolver?.RequestTagLength ?? 1024;
                    if (resolver != null && resolver.RequestContentAsTag &&
                        req.ContentLength != null &&
                        req.ContentLength < size &&
                        req.ContentType != null &&
                        req.ContentType.StartsWithIgnoreCase(TagTypes))
                    {
                        var buf = Pool.Shared.Rent(size);
                        try
                        {
                            req.EnableBuffering();

                            var count = await req.Body.ReadAsync(buf, 0, size).ConfigureAwait(false);
                            if (count > 0)
                            {
                                span.AppendTag("\r\n<=\r\n" + buf.ToStr(null, 0, count));
                                flag = true;
                            }
                            req.Body.Position = 0;
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
            await _next.Invoke(ctx).ConfigureAwait(false);

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
                        var size = resolver?.RequestTagLength ?? 1024;
                        if (resolver != null && resolver.RequestContentAsTag &&
                            res.ContentLength != null &&
                            res.ContentLength < size &&
                            res.Body.CanSeek &&
                            res.ContentType != null &&
                            res.ContentType.StartsWithIgnoreCase(TagTypes))
                        {
                            var buf = Pool.Shared.Rent(size);
                            try
                            {
                                var p = res.Body.Position;
                                var count = await res.Body.ReadAsync(buf, 0, size).ConfigureAwait(false);
                                if (count > 0)
                                {
                                    span.AppendTag("\r\n=>\r\n" + buf.ToStr(null, 0, count));
                                    flag = true;
                                }
                                res.Body.Position = p;
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

    private static DateTime _nextSave;
    private static ConcurrentDictionary<String, Int32> _serviceAddresses = new(StringComparer.OrdinalIgnoreCase);

    // 记录最近一次我们采纳的配置快照（本进程写入或外部修改后加载）
    private static String? _lastWrittenServiceAddress;

    // 归一化构造基地址：scheme://host[:port]，兼容IPv6加[]，端口仅在非默认时保留
    private static Boolean TryBuildBaseAddress(HttpContext ctx, out String? baseAddress)
    {
        baseAddress = null;

        var uri = ctx.Request.GetRawUrl();
        if (uri == null) return false;

        var host = uri.Authority;
        if (host.IsNullOrEmpty()) return false;

        var p = host.LastIndexOf(':');
        if (p >= 0) host = host[..p];

        var addr = $"{uri.Scheme}://{host}";
        if (uri.Port > 0)
        {
            if (uri.Scheme == "http" && uri.Port != 80)
                addr += ":" + uri.Port;
            else if (uri.Scheme == "https" && uri.Port != 443)
                addr += ":" + uri.Port;
        }

        baseAddress = addr;
        return true;
    }

    // 归一化配置中的地址为 scheme://host[:port]（IPv6自动加[]）
    private static String? NormalizeBaseAddress(String addr)
    {
        if (!Uri.TryCreate(addr, UriKind.Absolute, out var u)) return null;

        var host = u.Host;
        if (host.Contains(':') && !host.StartsWith("[")) host = $"[{host}]"; // IPv6

        var baseAddress = $"{u.Scheme}://{host}";
        var port = u.Port;
        if (port > 0)
        {
            if (u.Scheme == "http" && port != 80)
                baseAddress += ":" + port;
            else if (u.Scheme == "https" && port != 443)
                baseAddress += ":" + port;
        }

        return baseAddress;
    }

    // 外部修改时：清空内存计数，并按配置顺序赋 N*10 降序权重
    private static void ReloadCountsFromConfig(NewLife.Setting set)
    {
        _serviceAddresses.Clear();

        var csv = set.ServiceAddress;
        var list = new List<String>();
        if (!csv.IsNullOrEmpty())
        {
            var seen = new HashSet<String>(StringComparer.OrdinalIgnoreCase);
            var parts = csv!.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var item in parts)
            {
                var norm = NormalizeBaseAddress(item.Trim());
                if (!norm.IsNullOrEmpty() && seen.Add(norm)) list.Add(norm);
            }
        }

        var n = list.Count;
        for (var i = 0; i < n; i++)
        {
            _serviceAddresses[list[i]] = (n - i) * 10;
        }

        _lastWrittenServiceAddress = set.ServiceAddress?.Trim();
    }

    /// <summary>自动记录用户访问主机地址</summary>
    /// <param name="ctx"></param>
    public static void SaveServiceAddress(HttpContext ctx)
    {
        // 先检测外部修改：首次或变更即重载并赋权
        var set = NewLife.Setting.Current;
        var currentCfg = set.ServiceAddress?.Trim();
        if (_lastWrittenServiceAddress == null || currentCfg != _lastWrittenServiceAddress)
            ReloadCountsFromConfig(set);

        if (!TryBuildBaseAddress(ctx, out var baseAddress) || baseAddress == null) return;

        // 仅统计“本进程期间”的访问次数
        var count = _serviceAddresses.AddOrUpdate(baseAddress, 1, (k, v) => v + 1);

        // 节流：每地址每100次尝试一次，或每10分钟一次
        if (count % 100 != 1 && _nextSave >= DateTime.Now) return;

        _nextSave = DateTime.Now.AddMinutes(10);

        // 根据“本进程统计+（可能存在的）配置权重种子”选择 Top5
        var value = _serviceAddresses
            .OrderByDescending(e => e.Value)
            .Take(5)
            .Join(",", e => e.Key);

        if (!String.Equals(set.ServiceAddress, value, StringComparison.Ordinal))
        {
            DefaultSpan.Current?.AppendTag($"ServiceAddress: {value}");
            set.ServiceAddress = value;
            set.Save();
            _lastWrittenServiceAddress = value;
        }
    }
}