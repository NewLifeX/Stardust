using System.Net.Http;
using System.Text;
using NewLife;
using NewLife.Log;

namespace Stardust.Managers;

/// <summary>应用实例本地日志读取与上报。由 AppClient 注册 app/reportLog 指令。</summary>
/// <remarks>
/// 只读取应用自身日志目录下的当日日志文件，不接受平台传入任意路径；
/// 单次上报默认 256KB、上限 512KB，通过 StarServer 的 /Log 接口入库。
/// </remarks>
public static class AppLogCommands
{
    /// <summary>默认上报字节数</summary>
    public const Int32 DefaultTailBytes = 256 * 1024;

    /// <summary>单次上报字节数上限</summary>
    public const Int32 MaxTailBytes = 512 * 1024;

    /// <summary>解析当日日志文件路径。日志目录不存在时返回空</summary>
    /// <param name="logPath">日志目录或单文件路径</param>
    /// <param name="now">当前时间，决定当日文件名</param>
    /// <returns>日志文件完整路径；找不到时返回空</returns>
    public static String? ResolveLogFile(String? logPath, DateTime now)
    {
        if (logPath.IsNullOrEmpty()) return null;

        var path = logPath!.GetBasePath();

        // 单文件日志：配置直接指向文件
        if (File.Exists(path)) return path;
        if (!Directory.Exists(path)) return null;

        var file = Path.Combine(path, $"{now:yyyy_MM_dd}.log");
        if (File.Exists(file)) return file;

        // 文件名格式被配置改过时，退化为目录内最新日志
        return new DirectoryInfo(path).EnumerateFiles("*.log", SearchOption.TopDirectoryOnly)
            .OrderByDescending(e => e.LastWriteTimeUtc)
            .FirstOrDefault()?.FullName;
    }

    /// <summary>读取日志文件尾部</summary>
    /// <param name="file">日志文件完整路径</param>
    /// <param name="maxBytes">最大读取字节数，小于等于零时使用默认值</param>
    /// <returns>日志文本；文件不存在时返回空</returns>
    public static String? ReadTail(String? file, Int32 maxBytes)
    {
        if (file.IsNullOrEmpty() || !File.Exists(file)) return null;

        var fi = new FileInfo(file);
        var readLen = (Int32)Math.Min(maxBytes > 0 ? maxBytes : DefaultTailBytes, fi.Length);
        if (readLen <= 0) return "";

        var buff = new Byte[readLen];
        using var fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        fs.Seek(-readLen, SeekOrigin.End);

        var read = 0;
        while (read < readLen)
        {
            var n = fs.Read(buff, read, readLen - read);
            if (n <= 0) break;
            read += n;
        }

        return Encoding.UTF8.GetString(buff, 0, read);
    }

    /// <summary>构造日志上报请求。携带应用、实例与登录令牌，正文为原始日志文本</summary>
    /// <param name="baseUrl">星尘服务端地址，必须是 http/https</param>
    /// <param name="appId">应用标识</param>
    /// <param name="clientId">实例标识</param>
    /// <param name="token">登录令牌。为空时不携带</param>
    /// <param name="content">日志文本</param>
    /// <returns>HTTP 请求消息</returns>
    public static HttpRequestMessage BuildRequest(String baseUrl, String appId, String? clientId, String? token, String content)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, new Uri(baseUrl.TrimEnd('/') + "/Log"))
        {
            Content = new StringContent(content, Encoding.UTF8, "text/plain"),
        };
        req.Headers.Add("X-AppId", appId);
        if (!clientId.IsNullOrEmpty()) req.Headers.Add("X-ClientId", clientId);
        if (!token.IsNullOrEmpty()) req.Headers.Add("X-Token", token);

        return req;
    }

    /// <summary>从服务端 /Log 响应中解析已识别日志行数。非 JSON 或字段缺失时返回 0</summary>
    /// <param name="text">响应文本</param>
    /// <returns>已识别日志行数</returns>
    public static Int32 ParseParsedCount(String? text)
    {
        if (text.IsNullOrEmpty()) return 0;

        try
        {
            var dic = NewLife.Serialization.JsonParser.Decode(text!);
            if (dic != null && dic.TryGetValue("parsed", out var value) && value != null) return value.ToInt();
        }
        catch { }

        return 0;
    }
}

/// <summary>日志上报指令参数</summary>
public class AppLogReportArg
{
    /// <summary>最大读取字节数。默认 256KB，上限 512KB</summary>
    public Int32 MaxBytes { get; set; }
}

/// <summary>日志上报结果</summary>
public class AppLogReportResult
{
    /// <summary>日志文件名</summary>
    public String? File { get; set; }

    /// <summary>上报字节数</summary>
    public Int32 Bytes { get; set; }

    /// <summary>服务端已识别日志行数</summary>
    public Int32 Parsed { get; set; }
}
