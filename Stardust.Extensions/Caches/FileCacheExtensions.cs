using System;
using System.IO;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using NewLife;
using NewLife.Log;

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
    /// <param name="uplinkServer">上级地址，用于下载本地不存在的文件</param>
    /// <returns></returns>
    public static void UseFileCache(this IApplicationBuilder app, String requestPath, String localPath, String uplinkServer = null)
    {
        var cacheRoot = localPath.GetBasePath().EnsureDirectory(false);
        XTrace.WriteLine("FileCache: {0}", cacheRoot);

        var provider = new CacheFileProvider(cacheRoot, uplinkServer)
        {
            IndexInfoFile = "index.csv",
            Tracer = DefaultTracer.Instance
        };

        if (uplinkServer.IsNullOrEmpty())
        {
            var set = NewLife.Setting.Current;
            provider.GetServers = () => set.PluginServer?.Split(",");
        }
        else
        {
            XTrace.WriteLine("UplinkServer: {0}", uplinkServer);
        }

        app.UseStaticFiles(new StaticFileOptions
        {
            RequestPath = new PathString(requestPath),
            FileProvider = provider,
            ServeUnknownFileTypes = true,
            DefaultContentType = "application/x-msdownload",
        });
        app.UseDirectoryBrowser(new DirectoryBrowserOptions
        {
            RequestPath = new PathString(requestPath),
            FileProvider = provider,
        });
    }
}