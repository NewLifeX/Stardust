using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Text;
using NewLife;
using NewLife.Log;

namespace StarGateway.Proxy;

/*
 * 静态文件处理功能清单（相对常规中间件的完善项）：
 * 1. GET/HEAD：HEAD 只返回头部，不发送响应体（修复原先 HEAD 仍返回 body 的协议错误）
 * 2. Range 分片：支持 bytes=start-end / bytes=start- / bytes=-suffix，返回 206 + Content-Range，含 If-Range 校验
 * 3. 条件请求：ETag / Last-Modified + If-None-Match / If-Modified-Since，命中返回 304（不读文件）
 * 4. 缓存头：HTML 类 no-cache，其它资源 public, max-age=3600
 * 5. 安全头：统一 X-Content-Type-Options: nosniff；目录浏览 HTML 编码防 XSS；路径穿越防护收口（带尾分隔符校验）
 * 6. SPA 回退：路径无后缀或以 / 结尾且启用时，直接返回默认首页（支持 history 路由）；缺失的带后缀资源正确返回 404
 * 7. 目录无尾斜杠 301 重定向补斜杠（规范化）
 * 8. 大文件保护：全量读取受 MaxFileSize 限制，超出返回 413；Range 仅读所需分片
 * 9. MIME 类型补全（wasm / webm / mp4 / mp3 / csv / mjs / vue / otf / bmp / tiff / webmanifest 等）
 */

/// <summary>静态文件处理器。从本地文件系统读取文件并构建HTTP响应</summary>
public class StaticFileHandler
{
    #region 属性
    /// <summary>MIME类型映射</summary>
    private static readonly ConcurrentDictionary<String, String> _mimeTypes = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>日志</summary>
    public ILog Log { get; set; }

    /// <summary>是否启用日志</summary>
    public Boolean LogEnabled { get; set; }

    /// <summary>全量读取单文件的最大字节数（单位：字节）。默认 50MB（50 * 1024 * 1024 = 52,428,800 字节），超出返回 413。Range 分片只读取所需区间，不受此限制</summary>
    public Int64 MaxFileSize { get; set; } = 50 * 1024 * 1024;
    #endregion

    #region 构造
    static StaticFileHandler()
    {
        // 常用MIME类型
        _mimeTypes[".html"] = "text/html; charset=utf-8";
        _mimeTypes[".htm"] = "text/html; charset=utf-8";
        _mimeTypes[".css"] = "text/css; charset=utf-8";
        _mimeTypes[".js"] = "application/javascript; charset=utf-8";
        _mimeTypes[".mjs"] = "text/javascript; charset=utf-8";
        _mimeTypes[".json"] = "application/json; charset=utf-8";
        _mimeTypes[".webmanifest"] = "application/manifest+json";
        _mimeTypes[".xml"] = "application/xml; charset=utf-8";
        _mimeTypes[".txt"] = "text/plain; charset=utf-8";
        _mimeTypes[".csv"] = "text/csv; charset=utf-8";
        _mimeTypes[".vue"] = "text/plain; charset=utf-8";
        _mimeTypes[".svg"] = "image/svg+xml";
        _mimeTypes[".png"] = "image/png";
        _mimeTypes[".jpg"] = "image/jpeg";
        _mimeTypes[".jpeg"] = "image/jpeg";
        _mimeTypes[".gif"] = "image/gif";
        _mimeTypes[".bmp"] = "image/bmp";
        _mimeTypes[".tiff"] = "image/tiff";
        _mimeTypes[".ico"] = "image/x-icon";
        _mimeTypes[".webp"] = "image/webp";
        _mimeTypes[".woff"] = "font/woff";
        _mimeTypes[".woff2"] = "font/woff2";
        _mimeTypes[".ttf"] = "font/ttf";
        _mimeTypes[".otf"] = "font/otf";
        _mimeTypes[".eot"] = "application/vnd.ms-fontobject";
        _mimeTypes[".map"] = "application/json";
        _mimeTypes[".pdf"] = "application/pdf";
        _mimeTypes[".zip"] = "application/zip";
        _mimeTypes[".gz"] = "application/gzip";
        _mimeTypes[".wasm"] = "application/wasm";
        _mimeTypes[".webm"] = "video/webm";
        _mimeTypes[".mp4"] = "video/mp4";
        _mimeTypes[".mp3"] = "audio/mpeg";
    }
    #endregion

    #region 方法
    /// <summary>尝试处理静态文件请求（不接收请求头，Range/304 等能力不可用）</summary>
    public Boolean TryHandle(String method, String path, String staticRoot, String indexFile, Boolean directoryBrowse, Boolean spaFallback, out Byte[] response)
        => TryHandle(method, path, staticRoot, indexFile, directoryBrowse, spaFallback, null, out response);

    /// <summary>尝试处理静态文件请求</summary>
    /// <param name="method">HTTP方法</param>
    /// <param name="path">请求路径</param>
    /// <param name="staticRoot">静态文件根目录</param>
    /// <param name="indexFile">默认首页文件名</param>
    /// <param name="directoryBrowse">是否允许目录浏览</param>
    /// <param name="spaFallback">SPA回退。路径无后缀或以/结尾时直接返回默认首页，用于支持前端history路由模式</param>
    /// <param name="headers">请求头（用于 Range / If-Range / If-None-Match / If-Modified-Since 等）</param>
    /// <param name="response">HTTP响应字节数组，若返回true则包含完整响应</param>
    /// <returns>是否已处理（true=已处理，不需要继续转发）</returns>
    public Boolean TryHandle(String method, String path, String staticRoot, String indexFile, Boolean directoryBrowse, Boolean spaFallback, IDictionary<String, String> headers, out Byte[] response)
    {
        response = null;

        // 只处理 GET/HEAD
        if (!method.EqualIgnoreCase("GET", "HEAD")) return false;

        indexFile ??= "index.html";

        // 安全检查：根目录必须存在
        var root = Path.GetFullPath(staticRoot);
        if (!Directory.Exists(root))
        {
            WriteLog("静态文件根目录不存在: {0}", root);
            response = BuildError(404, "Not Found");
            return true;
        }
        // 根目录补尾分隔符，便于精确判定“仍在根目录下”
        var rootWithSep = root.EndsWith(Path.DirectorySeparatorChar) ? root : root + Path.DirectorySeparatorChar;

        // 规范化请求路径
        var requestPath = path;
        // 去除查询参数
        var qIndex = requestPath.IndexOf('?');
        if (qIndex >= 0) requestPath = requestPath[..qIndex];

        // 解码URL编码（仅一次，避免双重解码绕过）
        requestPath = Uri.UnescapeDataString(requestPath);

        // 用于 SPA 路由判定的原始路径（未补 index）
        var routePath = requestPath;

        // 路径以/结尾或为空，使用默认首页
        if (requestPath.EndsWith("/") || requestPath.IsNullOrEmpty())
        {
            requestPath += indexFile;
        }

        // 构建本地文件路径并规范化
        var localPath = root + requestPath.Replace('/', Path.DirectorySeparatorChar);
        localPath = Path.GetFullPath(localPath);

        // 安全检查：确保解析后的路径仍在根目录下（带尾分隔符，杜绝 C:\web 匹配 C:\webx 类边界）
        if (!localPath.StartsWith(rootWithSep, StringComparison.OrdinalIgnoreCase))
        {
            WriteLog("路径穿越攻击被阻止: {0}", path);
            response = BuildError(403, "Forbidden");
            return true;
        }

        // 是否是 SPA 路由型路径（无后缀或以/结尾）
        var routeLike = spaFallback && (routePath.EndsWith("/") || !Path.HasExtension(routePath));

        // 文件存在直接服务（含 Range/304/HEAD）
        if (File.Exists(localPath))
        {
            return ServeFile(localPath, method, headers, out response);
        }

        // 目录处理
        if (Directory.Exists(localPath))
        {
            // 目录未带尾斜杠 → 301 规范化补斜杠（客户端重试后走正常流程）
            if (!routePath.EndsWith("/"))
            {
                response = BuildRedirect(routePath + "/");
                return true;
            }

            if (directoryBrowse)
            {
                response = BuildDirectoryListing(requestPath, localPath, root);
                return true;
            }

            // 目录内默认页
            var dirIndex = Path.Combine(localPath, indexFile);
            if (File.Exists(dirIndex))
            {
                return ServeFile(dirIndex, method, headers, out response);
            }

            // SPA 路由型目录 → 回退默认首页
            if (routeLike)
            {
                var indexPath = Path.Combine(root, indexFile);
                if (File.Exists(indexPath))
                {
                    return ServeFile(indexPath, method, headers, out response);
                }
            }

            WriteLog("静态目录无默认页: {0}", localPath);
            response = BuildError(404, "Not Found");
            return true;
        }

        // 文件与目录都不存在：SPA 路由型路径回退默认首页，其余（含缺失的带后缀资源）正确 404
        if (routeLike)
        {
            var indexPath = Path.Combine(root, indexFile);
            if (File.Exists(indexPath))
            {
                if (LogEnabled)
                    WriteLog("SPA回退: {0} -> {1}", path, indexFile);

                return ServeFile(indexPath, method, headers, out response);
            }
        }

        WriteLog("静态文件不存在: {0}", localPath);
        response = BuildError(404, "Not Found");
        return true;
    }

    /// <summary>读取并构建单个文件的响应（统一处理 HEAD / Range / 304 / 缓存头 / 安全头）</summary>
    private Boolean ServeFile(String localPath, String method, IDictionary<String, String> headers, out Byte[] response)
    {
        response = null;
        try
        {
            var fileInfo = new FileInfo(localPath);
            var ext = Path.GetExtension(localPath);
            var contentType = _mimeTypes.GetValueOrDefault(ext, "application/octet-stream");
            var total = fileInfo.Length;
            var lastModified = fileInfo.LastWriteTimeUtc;
            var etag = $"\"{total}-{lastModified.Ticks}\"";

            var isHead = method.EqualIgnoreCase("HEAD");

            // 条件请求：If-None-Match / If-Modified-Since → 304
            var inm = GetHeader(headers, "If-None-Match");
            if (!inm.IsNullOrEmpty())
            {
                var a = etag.Trim('"');
                var b = inm.Trim('"');
                if (b.StartsWith("W/", StringComparison.OrdinalIgnoreCase)) b = b[2..];
                if (a == b) { response = BuildNotModified(etag, lastModified); return true; }
            }
            var ims = GetHeader(headers, "If-Modified-Since");
            if (!ims.IsNullOrEmpty() && DateTime.TryParse(ims, out var imsDate) && lastModified <= imsDate.ToUniversalTime().AddSeconds(1))
            {
                response = BuildNotModified(etag, lastModified);
                return true;
            }

            // Range 分片：206
            var isRange = false;
            Int64 start = 0, end = total - 1;
            var contentRange = "";
            var status = 200;
            var range = GetHeader(headers, "Range");
            if (!range.IsNullOrEmpty() && total > 0)
            {
                // If-Range 校验：与当前资源不匹配时忽略 Range，返回完整内容
                var applyRange = true;
                var ifRange = GetHeader(headers, "If-Range");
                if (!ifRange.IsNullOrEmpty() &&
                    ifRange != etag &&
                    !(DateTime.TryParse(ifRange, out var irDate) && lastModified <= irDate.ToUniversalTime().AddSeconds(1)))
                {
                    applyRange = false;
                }

                if (applyRange && ParseRange(range, total, out start, out end))
                {
                    isRange = true;
                    status = 206;
                    contentRange = $"bytes {start}-{end}/{total}";
                }
                else if (applyRange)
                {
                    // 无法满足的范围
                    response = BuildRangeNotSatisfiable(total);
                    return true;
                }
            }

            Byte[] data;
            Int64 contentLength;
            if (isHead)
            {
                // HEAD 不读取文件，仅返回头部
                data = Array.Empty<Byte>();
                contentLength = isRange ? end - start + 1 : total;
            }
            else if (isRange)
            {
                // 仅读取所需分片，避免大文件占用内存
                var count = (Int32)(end - start + 1);
                data = new Byte[count];
                using var fs = File.OpenRead(localPath);
                fs.Seek(start, SeekOrigin.Begin);
                var offset = 0;
                Int32 read;
                while (offset < count && (read = fs.Read(data, offset, count - offset)) > 0) offset += read;
                if (offset < count) Array.Resize(ref data, offset);
                contentLength = data.Length;
            }
            else
            {
                // 全量读取受最大文件限制保护
                if (total > MaxFileSize)
                {
                    response = BuildError(413, "Payload Too Large");
                    return true;
                }
                data = File.ReadAllBytes(localPath);
                contentLength = data.Length;
            }

            response = BuildFileResponse(status, contentType, data, contentLength, etag, lastModified, isRange, contentRange, isHead);

            if (LogEnabled)
                WriteLog("{0} {1} ({2}, {3})", isRange ? "206" : "200", localPath, contentType, GetSizeString(total));

            return true;
        }
        catch (Exception ex)
        {
            WriteLog("读取静态文件失败: {0} - {1}", localPath, ex.Message);
            response = BuildError(500, "Internal Server Error");
            return true;
        }
    }

    /// <summary>解析单个 Range 区间（bytes=start-end / bytes=start- / bytes=-suffix）</summary>
    private static Boolean ParseRange(String range, Int64 total, out Int64 start, out Int64 end)
    {
        start = 0;
        end = total - 1;
        if (!range.StartsWith("bytes=", StringComparison.OrdinalIgnoreCase)) return false;
        var spec = range["bytes=".Length..].Trim();
        // 仅支持单个区间
        if (spec.IndexOf(',') >= 0) return false;
        var parts = spec.Split('-');
        if (parts.Length != 2) return false;

        if (parts[0].Length == 0)
        {
            // 后缀形式：-500 → 最后 500 字节
            if (!Int64.TryParse(parts[1], out var suffix) || suffix <= 0) return false;
            start = Math.Max(0, total - suffix);
            end = total - 1;
        }
        else
        {
            if (!Int64.TryParse(parts[0], out start)) return false;
            if (parts[1].Length == 0)
                end = total - 1;
            else if (!Int64.TryParse(parts[1], out end)) return false;
        }

        if (start < 0 || end < start || start >= total) return false;
        if (end >= total) end = total - 1;
        return true;
    }

    /// <summary>不区分大小写读取请求头</summary>
    private static String GetHeader(IDictionary<String, String> headers, String name)
    {
        if (headers == null) return null;
        foreach (var kv in headers)
            if (kv.Key.EqualIgnoreCase(name)) return kv.Value;
        return null;
    }

    private static String StatusReason(Int32 code) => code switch
    {
        200 => "OK",
        206 => "Partial Content",
        301 => "Moved Permanently",
        304 => "Not Modified",
        403 => "Forbidden",
        404 => "Not Found",
        413 => "Payload Too Large",
        416 => "Range Not Satisfiable",
        500 => "Internal Server Error",
        _ => "OK"
    };

    private static Boolean IsNoCache(String contentType)
        => contentType.StartsWith("text/html", StringComparison.OrdinalIgnoreCase);

    private Byte[] BuildFileResponse(Int32 status, String contentType, Byte[] data, Int64 contentLength, String etag, DateTime lastModified, Boolean isRange, String contentRange, Boolean isHead)
    {
        var sb = new StringBuilder();
        sb.Append($"HTTP/1.1 {status} {StatusReason(status)}\r\n");
        sb.Append($"Content-Type: {contentType}\r\n");
        sb.Append($"Content-Length: {contentLength}\r\n");
        if (isRange) sb.Append($"Content-Range: {contentRange}\r\n");
        sb.Append("Accept-Ranges: bytes\r\n");
        sb.Append($"ETag: {etag}\r\n");
        sb.Append($"Last-Modified: {lastModified.ToUniversalTime():R}\r\n");
        sb.Append($"Cache-Control: {(IsNoCache(contentType) ? "no-cache" : "public, max-age=3600")}\r\n");
        sb.Append("X-Content-Type-Options: nosniff\r\n");
        sb.Append("Connection: keep-alive\r\n");
        sb.Append("\r\n");

        var headerBytes = Encoding.UTF8.GetBytes(sb.ToString());
        if (isHead || data.Length == 0) return headerBytes;

        var result = new Byte[headerBytes.Length + data.Length];
        Buffer.BlockCopy(headerBytes, 0, result, 0, headerBytes.Length);
        Buffer.BlockCopy(data, 0, result, headerBytes.Length, data.Length);
        return result;
    }

    private Byte[] BuildNotModified(String etag, DateTime lastModified)
    {
        var sb = new StringBuilder();
        sb.Append("HTTP/1.1 304 Not Modified\r\n");
        sb.Append($"ETag: {etag}\r\n");
        sb.Append($"Last-Modified: {lastModified.ToUniversalTime():R}\r\n");
        sb.Append("Cache-Control: no-cache\r\n");
        sb.Append("X-Content-Type-Options: nosniff\r\n");
        sb.Append("Connection: keep-alive\r\n");
        sb.Append("\r\n");
        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    private Byte[] BuildRangeNotSatisfiable(Int64 total)
    {
        var sb = new StringBuilder();
        sb.Append("HTTP/1.1 416 Range Not Satisfiable\r\n");
        sb.Append($"Content-Range: bytes */{total}\r\n");
        sb.Append("Content-Length: 0\r\n");
        sb.Append("Connection: close\r\n");
        sb.Append("\r\n");
        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    private Byte[] BuildRedirect(String location)
    {
        // 防止头注入
        location = location.Replace("\r", "").Replace("\n", "");
        var sb = new StringBuilder();
        sb.Append("HTTP/1.1 301 Moved Permanently\r\n");
        sb.Append($"Location: {location}\r\n");
        sb.Append("Content-Length: 0\r\n");
        sb.Append("Connection: close\r\n");
        sb.Append("\r\n");
        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    private Byte[] BuildError(Int32 statusCode, String message)
    {
        var body = $"<html><body><h1>{statusCode} {message}</h1></body></html>";
        var data = Encoding.UTF8.GetBytes(body);
        var sb = new StringBuilder();
        sb.Append($"HTTP/1.1 {statusCode} {message}\r\n");
        sb.Append("Content-Type: text/html; charset=utf-8\r\n");
        sb.Append($"Content-Length: {data.Length}\r\n");
        sb.Append("X-Content-Type-Options: nosniff\r\n");
        sb.Append("Connection: close\r\n");
        sb.Append("\r\n");

        var headerBytes = Encoding.UTF8.GetBytes(sb.ToString());
        var result = new Byte[headerBytes.Length + data.Length];
        Buffer.BlockCopy(headerBytes, 0, result, 0, headerBytes.Length);
        Buffer.BlockCopy(data, 0, result, headerBytes.Length, data.Length);

        return result;
    }

    private Byte[] BuildDirectoryListing(String requestPath, String dirPath, String rootPath)
    {
        var encPath = WebUtility.HtmlEncode(requestPath);
        var sb = new StringBuilder();
        sb.Append("<!DOCTYPE html><html><head><meta charset=\"utf-8\">");
        sb.Append($"<title>目录: {encPath}</title>");
        sb.Append("<style>body{font-family:sans-serif;margin:20px}li{padding:4px 0}a{text-decoration:none;color:#0366d6}a:hover{text-decoration:underline}</style>");
        sb.Append("</head><body>");
        sb.Append($"<h1>目录: {encPath}</h1><ul>");

        // 如果不是根目录，显示上级
        if (requestPath != "/")
        {
            var parent = requestPath.TrimEnd('/');
            var idx = parent.LastIndexOf('/');
            var parentPath = idx >= 0 ? parent[..idx] : "/";
            if (parentPath.IsNullOrEmpty()) parentPath = "/";
            sb.Append($"<li><a href=\"{WebUtility.HtmlEncode(parentPath)}\">..</a></li>");
        }

        try
        {
            var dir = new DirectoryInfo(dirPath);
            foreach (var d in dir.GetDirectories())
                sb.Append($"<li><a href=\"{WebUtility.HtmlEncode(requestPath.TrimEnd('/') + "/" + d.Name)}\">{WebUtility.HtmlEncode(d.Name)}/</a></li>");

            foreach (var f in dir.GetFiles())
                sb.Append($"<li><a href=\"{WebUtility.HtmlEncode(requestPath.TrimEnd('/') + "/" + f.Name)}\">{WebUtility.HtmlEncode(f.Name)}</a> ({GetSizeString(f.Length)})</li>");
        }
        catch (Exception ex)
        {
            sb.Append($"<li>读取目录失败: {WebUtility.HtmlEncode(ex.Message)}</li>");
        }

        sb.Append("</ul></body></html>");

        var data = Encoding.UTF8.GetBytes(sb.ToString());
        var header = new StringBuilder();
        header.Append("HTTP/1.1 200 OK\r\n");
        header.Append("Content-Type: text/html; charset=utf-8\r\n");
        header.Append($"Content-Length: {data.Length}\r\n");
        header.Append("X-Content-Type-Options: nosniff\r\n");
        header.Append("Connection: keep-alive\r\n");
        header.Append("\r\n");

        var headerBytes = Encoding.UTF8.GetBytes(header.ToString());
        var result = new Byte[headerBytes.Length + data.Length];
        Buffer.BlockCopy(headerBytes, 0, result, 0, headerBytes.Length);
        Buffer.BlockCopy(data, 0, result, headerBytes.Length, data.Length);

        return result;
    }

    private void WriteLog(String format, params Object[] args)
    {
        if (LogEnabled && Log != null)
            Log.Info(format, args);
    }

    private static String GetSizeString(Int64 size)
    {
        if (size < 1024) return $"{size} B";
        if (size < 1024 * 1024) return $"{size / 1024.0:F1} KB";
        if (size < 1024 * 1024 * 1024) return $"{size / (1024.0 * 1024):F1} MB";
        return $"{size / (1024.0 * 1024 * 1024):F2} GB";
    }
    #endregion
}
