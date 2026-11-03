using System;
using NewLife;

namespace Stardust.Data.Deployment;

/// <summary>webhook 解析公共方法。各平台解析器共用，保证分支名、删除标记与列宽截断口径一致</summary>
internal static class WebhookHelper
{
    /// <summary>剥离 refs/heads/ 前缀取得分支名。标签等其它引用原样返回</summary>
    /// <param name="gitRef">完整引用</param>
    /// <returns>分支名</returns>
    public static String GetBranch(String gitRef)
    {
        if (gitRef.IsNullOrEmpty()) return gitRef;

        const String prefix = "refs/heads/";
        return gitRef.StartsWithIgnoreCase(prefix) ? gitRef.Substring(prefix.Length) : gitRef;
    }

    /// <summary>是否为全零提交号。删除引用时 after 为全零</summary>
    /// <param name="sha">提交编号</param>
    /// <returns>是否为全零</returns>
    public static Boolean IsZeroSha(String sha)
    {
        if (sha.IsNullOrEmpty() || sha.Length != 40) return false;

        foreach (var ch in sha)
            if (ch != '0') return false;

        return true;
    }

    /// <summary>按数据库列宽截断。提交信息列 500、提交人列 100</summary>
    /// <param name="value">原始值</param>
    /// <param name="max">最大长度</param>
    /// <returns>截断后的值</returns>
    public static String Cut(String value, Int32 max) => value == null || value.Length <= max ? value : value.Substring(0, max);

    /// <summary>取第一个非空值。用于多个候选字段的优先级回退</summary>
    /// <param name="values">候选值</param>
    /// <returns>第一个非空值，全为空时返回 null</returns>
    public static String FirstNotEmpty(params String[] values)
    {
        if (values == null) return null;

        foreach (var value in values)
            if (!value.IsNullOrEmpty()) return value;

        return null;
    }
}
