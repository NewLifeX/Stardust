using System.Collections.Concurrent;
using NewLife;
using NewLife.Log;

namespace Stardust.Monitors;

/// <summary>星尘追踪解析器</summary>
public class StarTracerResolver : DefaultTracerResolver
{
    /// <summary>单个域名最大埋点数。默认16</summary>
    public Int32 MaxTracePerHost { get; set; } = 16;

    /// <summary>请求标签长度。HttpClient请求和WebApi请求响应作为数据标签的最大长度，小于0时不使用，默认1024字符</summary>
    public Int32 RequestTagLength { get; set; } = 1024;

    private ConcurrentDictionary<String, HashSet<String>> _cache = new();

    /// <summary>从Uri中解析出埋点名称</summary>
    /// <param name="uri"></param>
    /// <param name="userState"></param>
    /// <returns></returns>
    public override String ResolveName(Uri uri, Object? userState)
    {
        String name;
        HashSet<String>? keys = null;
        if (uri.IsAbsoluteUri)
        {
            // 域名下Http埋点过多时，埋点名称降级到域名，不再使用整个Url
            keys = _cache.GetOrAdd(uri.Host, k => []);
            if (keys.Count >= MaxTracePerHost) return $"{uri.Scheme}://{uri.Authority}";

            // 太长的Url分段，不适合作为埋点名称
            var segments = uri.Segments.Skip(1).TakeWhile(e => e.Length <= 16).ToArray();
            name = segments.Length > 0
               ? $"{uri.Scheme}://{uri.Authority}/{String.Concat(segments)}"
               : $"{uri.Scheme}://{uri.Authority}";
        }
        else
        {
            name = uri.ToString();
            var p = name.IndexOf('?');
            if (p > 0) name = name[..p];
        }

        name = ResolveName(name, userState);
        if (name.IsNullOrEmpty()) return name;

        keys?.Add(name);

        return name;
    }
}
