using NewLife;

namespace Stardust.Server.Services;

/// <summary>异常采样过滤器</summary>
/// <remarks>
/// 客户端提前断开连接、写请求体中断等传输层问题会被 ASP.NET Core 记为
/// <c>BadHttpRequestException</c> 等异常，进而被 SDK 统计为应用错误。
/// 这类失败属于请求方行为（HTTP 400 类），不是服务端业务故障，计入错误率会淹没真实异常，
/// 也会让错误率告警长期误报。命中规则的埋点仍然保留异常采样，仅把错误数清零，不丢失排障信息。
/// </remarks>
public static class ErrorSampleFilter
{
    /// <summary>默认忽略的异常关键字。逗号分隔，命中任意一项即视为传输层中断</summary>
    public const String DefaultKeywords = StarServerSetting.DefaultIgnoreErrorKeywords;

    /// <summary>判断错误信息是否为传输层中断，规则来自 <see cref="StarServerSetting.IgnoreErrorKeywords"/></summary>
    /// <param name="error">错误信息</param>
    /// <returns>是否传输层中断</returns>
    public static Boolean IsTransportAbort(String error) => IsTransportAbort(error, StarServerSetting.Current.IgnoreErrorKeywords);

    /// <summary>判断错误信息是否匹配忽略关键字</summary>
    /// <param name="error">错误信息</param>
    /// <param name="keywords">逗号分隔的关键字，为空时使用默认规则</param>
    /// <returns>是否传输层中断</returns>
    public static Boolean IsTransportAbort(String error, String keywords)
    {
        if (error.IsNullOrEmpty()) return false;

        if (keywords.IsNullOrEmpty()) keywords = DefaultKeywords;

        foreach (var item in keywords.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (error.Contains(item, StringComparison.OrdinalIgnoreCase)) return true;
        }

        return false;
    }

    /// <summary>剔除传输层中断产生的错误数，规则来自配置</summary>
    /// <param name="errors">原始错误数</param>
    /// <param name="messages">异常采样的错误信息</param>
    /// <returns>剔除后的错误数</returns>
    public static Int32 FixErrors(Int32 errors, IList<String> messages) => FixErrors(errors, messages, StarServerSetting.Current.IgnoreErrorKeywords);

    /// <summary>剔除传输层中断产生的错误数</summary>
    /// <remarks>采样数量不足以覆盖全部错误时不做判定，避免把未采样的真实错误一并忽略</remarks>
    /// <param name="errors">原始错误数</param>
    /// <param name="messages">异常采样的错误信息</param>
    /// <param name="keywords">逗号分隔的关键字</param>
    /// <returns>剔除后的错误数</returns>
    public static Int32 FixErrors(Int32 errors, IList<String> messages, String keywords)
    {
        if (errors <= 0) return errors;
        if (messages == null || messages.Count == 0) return errors;

        // 采样数量少于错误数，说明存在未被采样的错误，不能据此判定全部为传输层中断
        if (errors > messages.Count) return errors;

        foreach (var item in messages)
        {
            if (!IsTransportAbort(item, keywords)) return errors;
        }

        return 0;
    }
}
