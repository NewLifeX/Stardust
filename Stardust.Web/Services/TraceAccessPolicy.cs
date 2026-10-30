using NewLife;

namespace Stardust.Web.Services;

/// <summary>调用链访问方式</summary>
public enum TraceAccessModes
{
    /// <summary>拒绝访问</summary>
    None = 0,

    /// <summary>已登录用户</summary>
    User = 1,

    /// <summary>有效分享令牌</summary>
    Token = 2,

    /// <summary>站点开启匿名访问</summary>
    Anonymous = 3,
}

/// <summary>调用链访问判定</summary>
/// <remarks>三条通道：登录用户、分享令牌、匿名开关；前两者是常规路径，匿名开关仅限内网或演示部署</remarks>
public static class TraceAccessPolicy
{
    /// <summary>判定访问方式。优先级：登录用户 &gt; 分享令牌 &gt; 匿名开关</summary>
    /// <param name="signedIn">是否已登录</param>
    /// <param name="tokenValid">分享令牌是否有效</param>
    /// <param name="anonymous">站点是否允许匿名访问调用链</param>
    /// <returns>访问方式</returns>
    public static TraceAccessModes Evaluate(Boolean signedIn, Boolean tokenValid, Boolean anonymous)
    {
        if (signedIn) return TraceAccessModes.User;
        if (tokenValid) return TraceAccessModes.Token;
        if (anonymous) return TraceAccessModes.Anonymous;

        return TraceAccessModes.None;
    }

    /// <summary>解析追踪编号。支持逗号分隔的多个编号，以及W3C traceparent 四段格式</summary>
    /// <param name="id">原始编号，可能为 traceparent</param>
    /// <returns>追踪编号数组，无有效编号时返回空数组</returns>
    public static String[] ParseTraceIds(String id)
    {
        if (id.IsNullOrEmpty()) return [];

        var list = new List<String>();
        foreach (var item in id.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            // traceparent: 00-{traceId}-{spanId}-{flags}
            var ss = item.Split('-');
            var traceId = ss.Length == 4 ? ss[1] : item;
            if (!traceId.IsNullOrEmpty()) list.Add(traceId);
        }

        return list.Distinct().ToArray();
    }

    /// <summary>归一化为单个追踪编号。多个编号时取第一个</summary>
    /// <param name="id">原始编号，可能为 traceparent</param>
    /// <returns>追踪编号，无有效编号时返回null</returns>
    public static String NormalizeTraceId(String id) => ParseTraceIds(id).FirstOrDefault();
}
