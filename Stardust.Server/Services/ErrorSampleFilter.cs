using NewLife;
using NewLife.Collections;

namespace Stardust.Server.Services;

/// <summary>异常采样过滤器</summary>
/// <remarks>
/// 客户端提前断开连接、写请求体中断等传输层问题会被 ASP.NET Core 记为
/// <c>BadHttpRequestException</c> 等异常，进而被 SDK 统计为应用错误。
/// 这类失败属于请求方行为（HTTP 400 类），不是服务端业务故障，计入错误率会淹没真实异常，
/// 也会让错误率告警长期误报。命中规则的埋点仍然保留异常采样（压缩为摘要），仅把错误数清零，不丢失排障信息。
/// </remarks>
public static class ErrorSampleFilter
{
    /// <summary>默认忽略的异常关键字。逗号分隔，命中任意一项即视为传输层中断</summary>
    public const String DefaultKeywords = StarServerSetting.DefaultIgnoreErrorKeywords;

    /// <summary>WebSocket 对端未完成关闭握手等正常断连关键字。始终生效，兼容尚未补充该关键字的旧配置</summary>
    public const String WebSocketCloseKeywords = "The remote party closed the WebSocket connection,The WebSocket connection was closed unexpectedly";

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

        if (keywords.IsNullOrEmpty())
            keywords = DefaultKeywords;
        else
            keywords += "," + WebSocketCloseKeywords;

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

    /// <summary>压缩已知无害异常的采样内容。仅保留异常首行和第一个业务堆栈帧</summary>
    /// <remarks>
    /// 传输层中断的堆栈往往贯穿 Kestrel、ASP.NET Core 与框架内部，单条采样动辄数百字符，
    /// 既占满监控样本与告警消息，也淹没真正的业务异常。保留首行定位异常类型与消息，
    /// 再保留第一个非框架调用帧定位抛出位置，排障信息已经足够。
    /// </remarks>
    /// <param name="error">完整异常信息</param>
    /// <param name="keywords">逗号分隔的关键字，为空时使用默认规则</param>
    /// <returns>压缩后的摘要；非传输层中断返回原文</returns>
    public static String Summarize(String error, String keywords = null)
    {
        if (error.IsNullOrEmpty() || !IsTransportAbort(error, keywords)) return error;

        // 异常首行。单行堆栈在第一个 " at " 处截断
        var firstLine = error;
        var p = error.IndexOfAny(['\r', '\n']);
        if (p > 0) firstLine = error[..p];
        var index = firstLine.IndexOf(" at ", StringComparison.Ordinal);
        if (index > 0) firstLine = firstLine[..index];

        var sb = Pool.StringBuilder.Get();
        sb.Append(Cut(firstLine.Trim(), 200));

        var frame = FindFirstBusinessFrame(error);
        if (!frame.IsNullOrEmpty())
        {
            sb.AppendLine();
            sb.Append("   at ").Append(Cut(frame, 200));
        }

        return sb.Return(true);
    }

    private static String FindFirstBusinessFrame(String error)
    {
        var lines = error.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length > 1)
        {
            for (var i = 1; i < lines.Length && i < 16; i++)
            {
                var frame = NormalizeFrame(lines[i]);
                if (frame != null) return frame;
            }

            return null;
        }

        // 部分日志把堆栈压成单行，按 " at " 拆分
        var parts = error.Split(" at ", StringSplitOptions.RemoveEmptyEntries);
        for (var i = 1; i < parts.Length && i < 16; i++)
        {
            var frame = NormalizeFrame(parts[i]);
            if (frame != null) return frame;
        }

        return null;
    }

    private static String NormalizeFrame(String line)
    {
        var frame = line.Trim();
        if (frame.StartsWith("at ", StringComparison.Ordinal)) frame = frame[3..].Trim();
        if (frame.IsNullOrEmpty()) return null;

        // 跳过框架内部帧，优先定位到业务代码
        if (frame.StartsWith("System.", StringComparison.Ordinal) ||
            frame.StartsWith("Microsoft.", StringComparison.Ordinal) ||
            frame.StartsWith("NewLife.", StringComparison.Ordinal) ||
            frame.StartsWith("XCode.", StringComparison.Ordinal))
            return null;

        return frame;
    }

    private static String Cut(String value, Int32 maxLength)
    {
        if (value.IsNullOrEmpty() || value.Length <= maxLength) return value;

        return value[..maxLength] + "...";
    }

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
