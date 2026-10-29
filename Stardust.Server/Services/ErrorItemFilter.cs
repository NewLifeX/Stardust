using System.Text.RegularExpressions;
using NewLife;

namespace Stardust.Server.Services;

/// <summary>错误埋点过滤器</summary>
/// <remarks>
/// 连通性探测类埋点（例如旧版 SDK 的 rpc:Info 本地代理探测）在目标未启动时必然失败，
/// 这类失败不代表业务异常，计入应用错误率会淹没真实故障。命中忽略规则的埋点仍然保留
/// 调用次数、耗时与异常采样，仅把错误数清零，不丢失排障信息。
/// </remarks>
public static class ErrorItemFilter
{
    /// <summary>默认忽略的埋点。旧版 SDK 在未安装或未启动星尘代理的机器上探测 rpc:Info 必然失败</summary>
    public const String DefaultItems = StarServerSetting.DefaultIgnoreErrorItems;

    /// <summary>判断埋点是否忽略错误计数，规则来自 <see cref="StarServerSetting.IgnoreErrorItems"/></summary>
    /// <param name="name">埋点名称</param>
    /// <returns>是否忽略错误计数</returns>
    public static Boolean IsMatch(String name) => IsMatch(name, StarServerSetting.Current.IgnoreErrorItems);

    /// <summary>判断埋点是否匹配忽略规则</summary>
    /// <param name="name">埋点名称</param>
    /// <param name="patterns">逗号分隔的忽略规则，支持*和?通配，为空时使用默认规则</param>
    /// <returns>是否忽略错误计数</returns>
    public static Boolean IsMatch(String name, String patterns)
    {
        if (name.IsNullOrEmpty()) return false;

        if (patterns.IsNullOrEmpty()) patterns = DefaultItems;

        foreach (var item in patterns.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (item == "*") return true;
            if (!item.Contains('*') && !item.Contains('?'))
            {
                if (name.EqualIgnoreCase(item)) return true;
            }
            else
            {
                var reg = "^" + Regex.Escape(item).Replace("\\*", ".*").Replace("\\?", ".") + "$";
                if (Regex.IsMatch(name, reg, RegexOptions.IgnoreCase)) return true;
            }
        }

        return false;
    }
}
