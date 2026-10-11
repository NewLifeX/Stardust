using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
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
/// <param name="next"></param>
public class TracerMiddleware(RequestDelegate next)
{
    private readonly RequestDelegate _next = next ?? throw new ArgumentNullException(nameof(next));

    /// <summary>跟踪器</summary>
    public static ITracer? Tracer { get; set; }

    /// <summary>区域名集合。专用于魔方路由</summary>
    public static String[] AreaNames { get; set; } = [];

    /// <summary>启用埋点名称自动截断。默认true，以完整URL为基准，自动识别路径中的可变参数并截取前缀；关闭后采用传统两段或三段规则</summary>
    public static Boolean EnableAutoName { get; set; } = true;

    /// <summary>自动截断阈值。某前缀下不同子值数量达到该值即判定其后为可变参数区，默认16</summary>
    public static Int32 MinDistinct { get; set; } = 16;

    /// <summary>放行的请求路径规则。支持*模糊匹配，命中的路径保留完整URL作埋点名称，且不参与自动学习，如 /Admin/*、/v1/user/*。可直接追加</summary>
    public static IList<String> AllowPaths { get; set; } = [];

    /// <summary>自定义抛弃规则。返回true时丢弃该请求的埋点；为空时采用默认规则：未命中路由终结点的404响应</summary>
    /// <remarks>
    /// <para>默认规则针对扫描探测（如 /wp-login.php、/.env），这些请求不会命中任何路由终结点，埋点名又多又没有意义。
    /// 业务接口主动返回的404（如查询的资源不存在）会命中终结点，属于正常业务行为，仍然保留埋点。</para>
    /// <para>设置本属性后完全接管默认规则，需要保留默认行为时请在委托内自行包含。</para>
    /// </remarks>
    public static Func<HttpContext, Boolean>? ShouldAbandon { get; set; }

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

            // 采集请求路径，学习可变参数。仅本地统计，不上报
            if (EnableAutoName)
            {
                CollectPath(ctx.Request.Path + "");
                Consolidate();
            }

            if (!action.IsNullOrEmpty())
            {
                // 请求主体作为强制采样的数据标签，便于分析链路
                var req = ctx.Request;

                // 服务端片段需要继承上游链路。OpenTelemetry 的活动一旦启动就无法再指定父级，
                // 必须由 NewServerSpan 在创建时传入上游上下文，不能沿用"先建后 Detach"的写法
                span = Tracer.NewServerSpan(action, req.Headers);
                span!.Tag = $"{ctx.GetUserHost()} {req.Method} {req.GetRawUrl()}";
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

        // 验证Host是否允许
        if (!ValidateHost(ctx))
        {
            var code = HttpStatusCode.Forbidden;
            ctx.Response.StatusCode = (Int32)code;
            span?.SetError(new HttpRequestException($"Http Error {code} {(Int32)code}"), null);
            return;
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

                // 抛弃垃圾埋点。默认只丢弃未命中路由终结点的404，即路径不存在的扫描探测，避免TraceItem过多。
                // 业务接口主动返回的404（如查询的资源不存在）会命中终结点，属于正常业务行为，必须保留埋点
                if (ShouldAbandon?.Invoke(ctx) ?? (code == 404 && ctx.GetEndpoint() == null))
                    span.Abandon();
                else if (code == 400 || code > 404)
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
    private static readonly String[] CubeActions = ["index", "detail", "add", "edit", "delete", "deleteSelect", "deleteAll", "ExportCsv", "ExportExcel", "ExportZip", "Info", "SetEnable", "EnableSelect", "DisableSelect", "DeleteSelect"];

    /// <summary>从请求路径解析埋点名称</summary>
    /// <param name="ctx">HttpContext</param>
    /// <returns>埋点名称；静态资源等无需埋点的请求返回空</returns>
    /// <remarks>
    /// <para>默认采用自动截断：以完整URL为基准，识别路径中的可变参数段并截取前缀，学习结果写入配置文件跨进程复用。</para>
    /// <para>可通过 <see cref="EnableAutoName"/> 关闭自动截断，回退到传统规则：魔方保留三段，其它WebApi接口保留两段。</para>
    /// <para>最终名称交给 <see cref="ITracer.Resolver"/> 定制，解析器返回空时跳过该埋点。</para>
    /// </remarks>
    public static String? GetAction(HttpContext ctx)
    {
        var p = ctx.Request.Path + "";
        if (p.EndsWithIgnoreCase(ExcludeSuffixes)) return null;

        var name = EnableAutoName ? GetAutoName(p) : GetLegacyName(p);

        // 交由追踪解析器定制WebApi接口的埋点名称
        var resolver = Tracer?.Resolver;
        if (resolver != null)
        {
            var rs = resolver.ResolveName(name, ctx);
            if (rs.IsNullOrEmpty()) return null;
            name = rs!;
        }

        // 名称与跟踪数据Name字段保持一致，超长截断
        return name.Length > MaxNameLength ? name[..MaxNameLength] : name;
    }

    // 传统埋点名称规则：魔方区域或魔方动作保留三段，其它WebApi接口保留两段
    private static String GetLegacyName(String p)
    {
        var ss = p.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (ss.Length == 0) return p;

        // 如果是魔方格式，保留3段，其它webapi接口只留2段
        var rs = AreaNames;
        var n = rs != null && ss[0].EqualIgnoreCase(rs) || ss.Length >= 3 && ss[2].EqualIgnoreCase(CubeActions) ? 3 : 2;

        return ss.Length > n ? "/" + ss.Take(n).Join("/") : p;
    }

    // 自动埋点名称：以完整URL为基准，逐段下行，识别到可变节点后截断为前缀
    private static String GetAutoName(String p)
    {
        // 放行路径保留完整URL
        if (IsAllow(p)) return p;

        var ss = p.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (ss.Length == 0) return p;

        var prefixes = GetNamePrefixes();

        var parent = "";
        foreach (var item in ss)
        {
            // 父前缀已是可变节点，则截断为前缀。根节点不参与判定
            if (IsVariableNode(parent, prefixes)) return parent;

            parent = parent.Length == 0 ? "/" + item : parent + "/" + item;
        }

        return parent;
    }

    // 判断路径是否放行：魔方区域或自定义放行规则命中后，保留完整路径且不参与学习
    private static Boolean IsAllow(String path)
    {
        if (path.IsNullOrEmpty()) return false;

        // 魔方区域，首段命中即整体放行
        var rs = AreaNames;
        if (rs != null && rs.Length > 0)
        {
            var idx = path.IndexOf('/', 1);
            var first = idx < 0 ? path[1..] : path[1..idx];
            if (first.EqualIgnoreCase(rs)) return true;
        }

        // 自定义放行规则，支持*模糊匹配
        var ps = AllowPaths;
        if (ps != null && ps.Count > 0)
        {
            foreach (var item in ps)
            {
                if (!item.IsNullOrEmpty() && item.IsMatch(path, StringComparison.OrdinalIgnoreCase)) return true;
            }
        }

        return false;
    }

    // 判断前缀是否为可变节点：持久化前缀命中，或者内存统计达到阈值
    private static Boolean IsVariableNode(String parent, HashSet<String> prefixes)
    {
        // 根节点不参与截断，避免扫描类请求的随机首段把所有名称归并到根
        if (parent.Length == 0) return false;

        if (prefixes.Contains(parent)) return true;

        return _pathStats.TryGetValue(parent, out var node) && Volatile.Read(ref node.Distinct) >= MinDistinct;
    }

    // 埋点名称学习：路径前缀 -> 节点统计。仅统计本进程观察到的请求路径，用于识别可变参数
    private static readonly ConcurrentDictionary<String, PathNode> _pathStats = new();

    // 路径节点。按前缀记录访问次数与不同子值数量
    private sealed class PathNode
    {
        // 访问次数
        public Int32 Visits;

        // 不同子值数量。固定动作集合增长有限，参数值集合持续增长，达到阈值即判定该前缀之后为可变参数区
        public Int32 Distinct;
    }

    // 学习结果持久化间隔（秒）
    private const Int32 LearnInterval = 60;

    // 最多记录的路径节点数量，超限后不再学习，防止内存膨胀
    private const Int32 MaxNodes = 8192;

    // 埋点名称最大长度，与跟踪数据Name字段保持一致
    private const Int32 MaxNameLength = 200;

    private static DateTime _nextLearn;
    private static Int32 _nodeCount;

    // 采集请求路径，学习可变参数。仅本地统计，不上报；放行路径不参与学习
    private static void CollectPath(String path)
    {
        // 放行路径不计入统计，避免非参数的动作集合影响阈值
        if (IsAllow(path)) return;

        var ss = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (ss.Length == 0) return;

        // 超过节点上限，不再学习
        if (Volatile.Read(ref _nodeCount) >= MaxNodes) return;

        PathNode? parent = null;
        var key = "";
        foreach (var item in ss)
        {
            key = key.Length == 0 ? "/" + item : key + "/" + item;

            var node = _pathStats.GetOrAdd(key, static _ =>
            {
                Interlocked.Increment(ref _nodeCount);
                return new PathNode();
            });

            var visits = Interlocked.Increment(ref node.Visits);
            // 首次出现的子值：父节点不同子值数量加一。固定动作集合增长有限，参数值集合持续增长
            if (visits == 1 && parent != null) Interlocked.Increment(ref parent.Distinct);

            parent = node;
        }
    }

    // 巩固学习结果：把可变节点前缀追加到配置，供跨进程复用
    private static void Consolidate()
    {
        if (_nextLearn > DateTime.Now) return;
        _nextLearn = DateTime.Now.AddSeconds(LearnInterval);

        // 收集达到阈值的可变节点前缀
        var news = new List<String>();
        foreach (var item in _pathStats)
        {
            if (Volatile.Read(ref item.Value.Distinct) >= MinDistinct) news.Add(item.Key);
        }
        if (news.Count == 0) return;

        // 追加到配置
        var set = StarSetting.Current;
        var prefixes = GetNamePrefixes();
        var add = news.Where(e => !prefixes.Contains(e)).ToList();
        if (add.Count == 0) return;

        var value = set.TraceNamePrefixes;
        value = value.IsNullOrEmpty() ? add.Join(";") : value + ";" + add.Join(";");

        set.TraceNamePrefixes = value;
        try
        {
            set.Save();
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("保存埋点名称截断前缀失败：{0}", ex.Message);
        }

        _lastPrefixesConfig = null;
        XTrace.WriteLine("埋点名称截断前缀：{0}", value);
    }

    private static volatile HashSet<String>? _namePrefixes;
    private static String? _lastPrefixesConfig;

    // 读取配置中的埋点名称截断前缀。带缓存，配置变更后自动刷新
    private static HashSet<String> GetNamePrefixes()
    {
        var value = StarSetting.Current.TraceNamePrefixes;
        if (_lastPrefixesConfig == value && _namePrefixes != null) return _namePrefixes;

        var set = new HashSet<String>(StringComparer.OrdinalIgnoreCase);
        if (!value.IsNullOrEmpty())
        {
            foreach (var item in value.Split([';', ','], StringSplitOptions.RemoveEmptyEntries))
            {
                set.Add(item.Trim());
            }
        }

        _lastPrefixesConfig = value;
        _namePrefixes = set;

        return set;
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
    private static void ReloadCountsFromConfig(StarSetting set)
    {
        _serviceAddresses.Clear();

        var address = set.ServiceAddress?.Trim();
        var list = new List<String>();
        if (!address.IsNullOrEmpty())
        {
            var seen = new HashSet<String>(StringComparer.OrdinalIgnoreCase);
            var parts = address.Split([','], StringSplitOptions.RemoveEmptyEntries);
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

        _lastWrittenServiceAddress = address;

        // 更新星尘工厂
        if (Tracer is StarTracer st && st.Factory != null)
            st.Factory.ExternalAddress = address;
    }

    /// <summary>验证请求的Host是否允许</summary>
    /// <param name="ctx">HttpContext</param>
    /// <returns>验证通过返回true，否则返回false</returns>
    private static Boolean ValidateHost(HttpContext ctx)
    {
        var set = StarSetting.Current;
        var allowedHosts = set.AllowedHosts?.Trim();

        // 如果未配置允许的主机，则不进行验证
        if (allowedHosts.IsNullOrEmpty()) return true;

        // 获取请求的Host（包含端口）
        var host = ctx.Request.Host.Host;
        if (host.IsNullOrEmpty()) return false;

        // IP地址请求直接放行
        if (IPAddress.TryParse(host, out _)) return true;

        // 验证Host是否在允许列表中。IsMatch的接收者是模式串，配置项作为模式，请求Host作为输入
        var allowedList = allowedHosts.Split([','], StringSplitOptions.RemoveEmptyEntries);
        foreach (var allowed in allowedList)
        {
            if (allowed.IsMatch(host, StringComparison.OrdinalIgnoreCase)) return true;
            //if (host.EqualIgnoreCase(allowed.Trim())) return true;
        }

        return false;
    }

    /// <summary>自动记录用户访问主机地址</summary>
    /// <param name="ctx"></param>
    public static void SaveServiceAddress(HttpContext ctx)
    {
        // 先检测外部修改：首次或变更即重载并赋权
        var set = StarSetting.Current;
        var currentCfg = set.ServiceAddress?.Trim();
        if (_lastWrittenServiceAddress == null || currentCfg != _lastWrittenServiceAddress)
            ReloadCountsFromConfig(set);

        if (!TryBuildBaseAddress(ctx, out var baseAddress) || baseAddress == null) return;

        // 仅统计“本进程期间”的访问次数
        var count = _serviceAddresses.AddOrUpdate(baseAddress, 1, (k, v) => v + 1);

        // 节流：每地址每100次尝试一次，或每10分钟一次
        if (count % 100 != 1 && _nextSave >= DateTime.Now) return;

        _nextSave = DateTime.Now.AddMinutes(1);

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

        // 更新星尘工厂
        if (Tracer is StarTracer st && st.Factory != null)
            st.Factory.ExternalAddress = value;
    }
}