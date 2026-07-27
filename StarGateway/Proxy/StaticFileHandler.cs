using System.Collections.Concurrent;
using System.Text;
using NewLife;
using NewLife.Log;

namespace StarGateway.Proxy;

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
    #endregion

    #region 构造
    static StaticFileHandler()
    {
        // 常用MIME类型
        _mimeTypes[".html"] = "text/html; charset=utf-8";
        _mimeTypes[".htm"] = "text/html; charset=utf-8";
        _mimeTypes[".css"] = "text/css; charset=utf-8";
        _mimeTypes[".js"] = "application/javascript; charset=utf-8";
        _mimeTypes[".json"] = "application/json; charset=utf-8";
        _mimeTypes[".xml"] = "application/xml; charset=utf-8";
        _mimeTypes[".txt"] = "text/plain; charset=utf-8";
        _mimeTypes[".svg"] = "image/svg+xml";
        _mimeTypes[".png"] = "image/png";
        _mimeTypes[".jpg"] = "image/jpeg";
        _mimeTypes[".jpeg"] = "image/jpeg";
        _mimeTypes[".gif"] = "image/gif";
        _mimeTypes[".ico"] = "image/x-icon";
        _mimeTypes[".webp"] = "image/webp";
        _mimeTypes[".woff"] = "font/woff";
        _mimeTypes[".woff2"] = "font/woff2";
        _mimeTypes[".ttf"] = "font/ttf";
        _mimeTypes[".eot"] = "application/vnd.ms-fontobject";
        _mimeTypes[".map"] = "application/json";
        _mimeTypes[".pdf"] = "application/pdf";
        _mimeTypes[".zip"] = "application/zip";
        _mimeTypes[".gz"] = "application/gzip";
    }
    #endregion

    #region 方法
    /// <summary>尝试处理静态文件请求</summary>
    /// <param name="method">HTTP方法</param>
    /// <param name="path">请求路径</param>
    /// <param name="staticRoot">静态文件根目录</param>
    /// <param name="indexFile">默认首页文件名</param>
    /// <param name="directoryBrowse">是否允许目录浏览</param>
    /// <param name="spaFallback">SPA回退。文件不存在时回退到index.html，用于支持前端history路由模式</param>
    /// <param name="response">HTTP响应字节数组，若返回true则包含完整响应</param>
    /// <returns>是否已处理（true=已处理，不需要继续转发）</returns>
    public Boolean TryHandle(String method, String path, String staticRoot, String indexFile, Boolean directoryBrowse, Boolean spaFallback, out Byte[] response)
    {
        response = null;

        // 只处理 GET/HEAD
        if (!method.EqualIgnoreCase("GET", "HEAD")) return false;

        // 安全检查：防止路径穿越
        var root = Path.GetFullPath(staticRoot);
        if (!Directory.Exists(root))
        {
            WriteLog("静态文件根目录不存在: {0}", root);
            response = BuildError(404, "Not Found");
            return true;
        }

        // 规范化请求路径
        var requestPath = path;
        // 去除查询参数
        var qIndex = requestPath.IndexOf('?');
        if (qIndex >= 0) requestPath = requestPath[..qIndex];

        // 解码URL编码
        requestPath = Uri.UnescapeDataString(requestPath);

        // 路径以/结尾，使用默认首页
        if (requestPath.EndsWith("/") || requestPath.IsNullOrEmpty())
        {
            requestPath += indexFile ?? "index.html";
        }

        // 构建本地文件路径
        var localPath = root + requestPath.Replace('/', Path.DirectorySeparatorChar);
        localPath = Path.GetFullPath(localPath);

        // 安全检查：确保解析后的路径仍在根目录下
        if (!localPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            WriteLog("路径穿越攻击被阻止: {0}", path);
            response = BuildError(403, "Forbidden");
            return true;
        }

        // 检查文件是否存在
        if (!File.Exists(localPath))
        {
            // 如果是目录且允许浏览
            if (directoryBrowse && Directory.Exists(localPath))
            {
                response = BuildDirectoryListing(requestPath, localPath, root);
                return true;
            }

            // SPA回退：文件不存在时回退到index.html
            if (spaFallback)
            {
                var indexPath = Path.Combine(root, indexFile ?? "index.html");
                if (File.Exists(indexPath))
                {
                    if (LogEnabled)
                        WriteLog("SPA回退: {0} -> {1}", path, indexFile ?? "index.html");

                    return TryHandle(method, "/" + (indexFile ?? "index.html"), staticRoot, indexFile, false, false, out response);
                }
            }

            WriteLog("静态文件不存在: {0}", localPath);
            response = BuildError(404, "Not Found");
            return true;
        }

        try
        {
            var fileInfo = new FileInfo(localPath);
            var ext = Path.GetExtension(localPath);
            var contentType = _mimeTypes.GetValueOrDefault(ext, "application/octet-stream");

            // 读取文件内容
            var data = File.ReadAllBytes(localPath);

            // 构建响应
            var sb = new StringBuilder();
            sb.Append("HTTP/1.1 200 OK\r\n");
            sb.Append($"Content-Type: {contentType}\r\n");
            sb.Append($"Content-Length: {data.Length}\r\n");
            sb.Append("Accept-Ranges: bytes\r\n");
            sb.Append("Connection: keep-alive\r\n");
            sb.Append("\r\n");

            var headerBytes = Encoding.UTF8.GetBytes(sb.ToString());
            var result = new Byte[headerBytes.Length + data.Length];
            Buffer.BlockCopy(headerBytes, 0, result, 0, headerBytes.Length);
            Buffer.BlockCopy(data, 0, result, headerBytes.Length, data.Length);

            response = result;

            if (LogEnabled)
                WriteLog("200 {0} ({1}, {2})", path, contentType, GetSizeString(fileInfo.Length));

            return true;
        }
        catch (Exception ex)
        {
            WriteLog("读取静态文件失败: {0} - {1}", localPath, ex.Message);
            response = BuildError(500, "Internal Server Error");
            return true;
        }
    }

    private Byte[] BuildError(Int32 statusCode, String message)
    {
        var body = $"<html><body><h1>{statusCode} {message}</h1></body></html>";
        var data = Encoding.UTF8.GetBytes(body);
        var sb = new StringBuilder();
        sb.Append($"HTTP/1.1 {statusCode} {message}\r\n");
        sb.Append("Content-Type: text/html; charset=utf-8\r\n");
        sb.Append($"Content-Length: {data.Length}\r\n");
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
        var sb = new StringBuilder();
        sb.Append("<!DOCTYPE html><html><head><meta charset=\"utf-8\">");
        sb.Append($"<title>目录: {requestPath}</title>");
        sb.Append("<style>body{font-family:sans-serif;margin:20px}li{padding:4px 0}a{text-decoration:none;color:#0366d6}a:hover{text-decoration:underline}</style>");
        sb.Append("</head><body>");
        sb.Append($"<h1>目录: {requestPath}</h1><ul>");

        // 如果不是根目录，显示上级
        if (requestPath != "/")
        {
            var parent = requestPath.TrimEnd('/');
            var idx = parent.LastIndexOf('/');
            var parentPath = idx >= 0 ? parent[..idx] : "/";
            if (parentPath.IsNullOrEmpty()) parentPath = "/";
            sb.Append($"<li><a href=\"{parentPath}\">..</a></li>");
        }

        try
        {
            var dir = new DirectoryInfo(dirPath);
            foreach (var d in dir.GetDirectories())
                sb.Append($"<li><a href=\"{requestPath.TrimEnd('/')}/{d.Name}\">{d.Name}/</a></li>");

            foreach (var f in dir.GetFiles())
                sb.Append($"<li><a href=\"{requestPath.TrimEnd('/')}/{f.Name}\">{f.Name}</a> ({GetSizeString(f.Length)})</li>");
        }
        catch (Exception ex)
        {
            sb.Append($"<li>读取目录失败: {ex.Message}</li>");
        }

        sb.Append("</ul></body></html>");

        var data = Encoding.UTF8.GetBytes(sb.ToString());
        var header = new StringBuilder();
        header.Append("HTTP/1.1 200 OK\r\n");
        header.Append("Content-Type: text/html; charset=utf-8\r\n");
        header.Append($"Content-Length: {data.Length}\r\n");
        header.Append("Connection: close\r\n");
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
