using System.Collections.Concurrent;
using NewLife;
using NewLife.Log;

namespace Stardust.Monitors;

/// <summary>星尘追踪解析器。从 HTTP 请求 Uri 中解析埋点名称，控制单个域名的埋点基数，防止高基数爆炸</summary>
/// <remarks>
/// <para>继承自 <see cref="DefaultTracerResolver"/>，在基类基础上增加以下策略：</para>
/// <list type="bullet">
/// <item><see cref="MaxTracePerHost"/>：限制每个域名下最多记录多少个不同的埋点名称（默认 64），
/// 超过后该域名的后续请求统一降级为 <c>{scheme}://{host}</c>，避免高基数导致监控存储膨胀。</item>
/// <item>URI 分段截断：每个路径段超过 16 字符的部分被丢弃，只保留短路径段用于构建埋点名称。</item>
/// <item>相对 URI：去掉查询参数 <c>?xxx</c> 后再解析。</item>
/// </list>
/// <para>线程安全：内部使用 <c>ConcurrentDictionary</c> 实现无锁并发访问，适用于高并发 HTTP 诊断监听场景。</para>
/// <para>典型调用链：<c>HttpDiagnosticListener</c> 监听 <c>HttpClient</c> 发出的每个请求，
/// 通过 <c>DefaultTracerResolver.CreateSpan</c> → 本类 <c>ResolveName</c> 生成埋点名称。</para>
/// </remarks>
public class StarTracerResolver : DefaultTracerResolver
{
    #region 属性
    /// <summary>单个域名最大埋点数。超过此数量后该域名下的新埋点名称降级为域名本身，默认64</summary>
    /// <remarks>防止高基数：例如 API 路径中带 ID 时每个 ID 生成一个独立埋点，设置上限后可避免监控数据爆炸。</remarks>
    public Int32 MaxTracePerHost { get; set; } = 64;

    /// <summary>请求/响应标签最大长度。HttpClient 请求体和 WebApi 请求响应的正文作为数据标签时的截断长度，小于 0 时不捕获正文，默认 1024 字符</summary>
    /// <remarks>
    /// <para>该属性在 <c>TracerMiddleware</c> 中被读取，用于限制捕获的请求/响应正文大小。</para>
    /// <para>服务端可通过 <c>TraceResponse.RequestTagLength</c> 动态下发该值。</para>
    /// </remarks>
    public Int32 RequestTagLength { get; set; } = 1024;
    #endregion

    #region 静态
    /// <summary>每个域名已记录的埋点名称集合。Key=域名（Host），Value=该域名下已出现的埋点名称去重集合</summary>
    /// <remarks>
    /// 外层和内层均使用 <see cref="ConcurrentDictionary{TKey,TValue}"/> 保证线程安全：
    /// 内层 Value 类型为 <c>ConcurrentDictionary&lt;String, Byte&gt;</c>，利用其 Key 做去重集合（Value 仅占位），
    /// 替代非线程安全的 <see cref="HashSet{T}"/>，避免并发 Add 导致 <c>InvalidOperationException</c>。
    /// </remarks>
    private ConcurrentDictionary<String, ConcurrentDictionary<String, Byte>> _cache = new();
    #endregion

    #region 方法
    /// <summary>从 Uri 中解析出埋点名称。在基类基础上增加域名限流和 URI 分段截断策略</summary>
    /// <param name="uri">HTTP 请求的 URI</param>
    /// <param name="userState">用户自定义状态对象，传递给基类 <see cref="DefaultTracerResolver.ResolveName(String, Object)"/></param>
    /// <returns>解析后的埋点名称；若基类返回空则返回空</returns>
    /// <remarks>
    /// <para>解析步骤：</para>
    /// <list type="number">
    /// <item>绝对 URI：以 <c>{scheme}://{host}</c> 为前缀，拼接首个路径段中长度 &lt;=16 的部分。
    /// 若该域名已有埋点数达到 <see cref="MaxTracePerHost"/>（默认 64），直接返回 <c>{scheme}://{host}</c> 不拼接路径。</item>
    /// <item>相对 URI：去掉查询参数后作为原始名称。</item>
    /// <item>调用基类 <see cref="DefaultTracerResolver.ResolveName(String, Object)"/> 做最终解析。</item>
    /// <item>将解析后的名称加入该域名的去重集合，供后续计数限流。</item>
    /// </list>
    /// </remarks>
    public override String? ResolveName(Uri uri, Object? userState)
    {
        String? name;
        ConcurrentDictionary<String, Byte>? keys = null;
        if (uri.IsAbsoluteUri)
        {
            // 获取或创建该域名的埋点名称集合（ConcurrentDictionary 作为并发安全集合使用）
            keys = _cache.GetOrAdd(uri.Host, k => new ConcurrentDictionary<String, Byte>());
            // 域名下埋点过多时，降级为仅域名级别，不再细分具体路径，防止高基数
            if (keys.Count >= MaxTracePerHost) return $"{uri.Scheme}://{uri.Authority}";

            // 太长的 URI 路径段不适合作为埋点名称，仅取长度 <= 16 的段
            var segments = uri.Segments.Skip(1).TakeWhile(e => e.Length <= 16).ToArray();
            name = segments.Length > 0
               ? $"{uri.Scheme}://{uri.Authority}/{String.Concat(segments)}"
               : $"{uri.Scheme}://{uri.Authority}";
        }
        else
        {
            // 相对 URI：去除查询参数后作为原始名称
            name = uri.ToString();
            var p = name.IndexOf('?');
            if (p > 0) name = name[..p];
        }

        // 委托基类做名称规范化处理（如去除尾部斜杠、统一大小写等）
        name = ResolveName(name, userState);
        if (name.IsNullOrEmpty()) return name;

        // 将解析后的名称加入该域名的去重集合，TryAdd 保证并发安全
        keys?.TryAdd(name, 0);

        return name;
    }
    #endregion
}
