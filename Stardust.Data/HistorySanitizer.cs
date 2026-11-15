using System;
using System.Text.RegularExpressions;
using NewLife;

namespace Stardust.Data;

/// <summary>审计历史脱敏。登录请求会携带密钥、口令与令牌，写入历史备注前统一掩码，避免凭据进入审计库。</summary>
/// <remarks>
/// 节点与应用历史会保存登录/注册请求的 JSON 报文，报文中包含 <c>Secret</c> 字段。
/// 历史列表可被所有具备查看权限的账号访问，也可能被导出，因此必须在写入前脱敏而不是仅在页面隐藏。
/// 兼容两类写法：JSON 的 <c>"Secret":"..."</c> 与查询串/日志的 <c>secret=...</c>。
/// </remarks>
public static class HistorySanitizer
{
    private const String Mask = "***";

    private static readonly Regex _jsonField = new(
        "(?i)(\"(?:secret|password|passwd|pwd|token|access_token|refresh_token|appsecret|privatekey|apikey|api_key|clientsecret)\"\\s*:\\s*\")(?:[^\"\\\\]|\\\\.)*(\")",
        RegexOptions.Compiled);

    private static readonly Regex _keyValue = new(
        "(?i)(\\b(?:secret|password|passwd|pwd|token|appsecret|privatekey|apikey|api_key|clientsecret)\\b\\s*=\\s*)([^\\s,;&}\\]]+)",
        RegexOptions.Compiled);

    /// <summary>掩码备注中的密钥、口令与令牌字段，保留字段名与其余可读内容</summary>
    /// <param name="remark">原始备注，可能为 null 或空字符串</param>
    /// <returns>脱敏后的备注；入参为空时原样返回</returns>
    /// <example>
    /// <code>
    /// var remark = HistorySanitizer.Redact("{\"Code\":\"app\",\"Secret\":\"$md5$1$ABC\"}");
    /// // {"Code":"app","Secret":"***"}
    /// </code>
    /// </example>
    public static String Redact(String remark)
    {
        if (remark.IsNullOrEmpty()) return remark;

        var text = _jsonField.Replace(remark, $"$1{Mask}$2");
        return _keyValue.Replace(text, $"$1{Mask}");
    }
}
