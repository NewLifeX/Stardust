using System;
using System.IO;
using System.Linq;
using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
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
            // 拼接上游地址时需要补回请求虚拟路径，否则会请求到站点根目录
            RequestPath = requestPath,
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

        // 记录本进程监听端口，供自环识别使用。未取到时退化为按主机判断
        FillLocalPorts(app);
        CheckSelfUplink(provider);

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

    /// <summary>填充本进程监听端口，用于识别上游地址是否指向自身</summary>
    /// <param name="app"></param>
    static void FillLocalPorts(IApplicationBuilder app)
    {
        try
        {
            var server = app.ApplicationServices.GetService(typeof(IServer)) as IServer;
            var feature = server?.Features.Get<IServerAddressesFeature>();
            if (feature == null) return;

            foreach (var item in feature.Addresses)
            {
                if (Uri.TryCreate(item.Replace("*", "0.0.0.0").Replace("+", "0.0.0.0"), UriKind.Absolute, out var uri) && uri.Port > 0)
                    CacheFileProvider.LocalPorts.Add(uri.Port);
            }
        }
        catch (Exception ex)
        {
            XTrace.WriteLine("获取本地监听端口失败：{0}", ex.Message);
        }
    }

    /// <summary>启动自检。上游指向自身时，拉取文件会变成自己请求自己的死循环，直接告警</summary>
    /// <param name="provider"></param>
    static void CheckSelfUplink(CacheFileProvider provider)
    {
        var svrs = provider.GetServers?.Invoke() ?? provider.Servers;
        if (svrs == null || svrs.Length == 0) return;

        var selfs = svrs.Where(e => !e.IsNullOrEmpty() && CacheFileProvider.IsSelf(e)).ToList();
        if (selfs.Count == 0) return;

        XTrace.WriteLine("警告：文件缓存上游[{0}]指向自身，已禁用该上游的文件拉取，请检查配置项PluginServer", selfs.Join());

        // 剔除指向自身的上游，避免每次请求都空等超时
        provider.Servers = svrs.Where(e => !CacheFileProvider.IsSelf(e)).ToArray();

        var getter = provider.GetServers;
        if (getter != null) provider.GetServers = () => getter().Where(e => !CacheFileProvider.IsSelf(e)).ToArray();
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