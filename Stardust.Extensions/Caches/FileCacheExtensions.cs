using System;
using System.IO;
using System.Linq;
using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.StaticFiles;
using NewLife;
using NewLife.Log;
using NewLife.Web;

namespace Stardust.Extensions.Caches;

/// <summary>
/// 文件缓存扩展
/// </summary>
public static class FileCacheExtensions
{
    /// <summary>使用文件缓存，把指定目录映射到url上，并提供上级地址用于下载不存在的文件</summary>
    /// <param name="app"></param>
    /// <param name="requestPath">请求虚拟路径。如/files</param>
    /// <param name="localPath">本地缓存目录</param>
    /// <param name="getWhiteIP"></param>
    /// <param name="uplinkServer">上级地址，用于下载本地不存在的文件</param>
    /// <returns></returns>
    public static void UseFileCache(this IApplicationBuilder app, String requestPath, String localPath, Func<String>? getWhiteIP = null, String? uplinkServer = null)
    {
        var cacheRoot = localPath.GetBasePath().EnsureDirectory(false);
        XTrace.WriteLine("FileCache: {0}", cacheRoot);

        var provider = new CacheFileProvider(cacheRoot, uplinkServer)
        {
            IndexInfoFile = "index.csv",
            //Tracer = DefaultTracer.Instance,
            ServiceProvider = app.ApplicationServices,
        };

        if (uplinkServer.IsNullOrEmpty())
        {
            var set = NewLife.Setting.Current;
            provider.GetServers = () => set.PluginServer?.Split(",") ?? new String[0];
        }
        else
        {
            XTrace.WriteLine("UplinkServer: {0}", uplinkServer);
        }

        // IP白名单拦截，必须在使用静态文件前面
        if (getWhiteIP != null)
            app.UseMiddleware<IpFilterMiddleware>(requestPath, getWhiteIP());

        app.UseStaticFiles(new StaticFileOptions
        {
            RequestPath = new PathString(requestPath),
            FileProvider = provider,
            ServeUnknownFileTypes = true,
            DefaultContentType = "application/x-msdownload",
            OnPrepareResponse = ctx => OnPrepareResponse(ctx, getWhiteIP),
        });
        app.UseDirectoryBrowser(new DirectoryBrowserOptions
        {
            RequestPath = new PathString(requestPath),
            FileProvider = provider,
        });
    }

    static void OnPrepareResponse(StaticFileResponseContext ctx, Func<String>? getWhiteIP)
    {
        var ip = ctx.Context.GetUserHost();
        if (ip.IsNullOrEmpty() || !ValidIP(ip, getWhiteIP))
        {
            ctx.Context.Response.StatusCode = (Int32)HttpStatusCode.Forbidden;
            ctx.Context.Response.ContentLength = 0;
            ctx.Context.Response.Body = Stream.Null;
        }
    }

    static Boolean ValidIP(String ip, Func<String>? getWhiteIP)
    {
        if (ip.IsNullOrEmpty()) return false;

        var whites = getWhiteIP?.Invoke();
        if (whites.IsNullOrEmpty()) return true;

        // 白名单里面有的，直接通过
        var ws = (whites + "").Split(",", ";");
        if (ws.Length > 0)
        {
            return ws.Any(e => e.IsMatch(ip));
        }

        // 未设置白名单，黑名单里面没有的，直接通过
        return true;
    }
}