using System.ComponentModel;
using System.Diagnostics;
using NewLife;
using NewLife.Caching;
using NewLife.Configuration;
using NewLife.Log;
using NewLife.Model;

namespace Stardust;

/// <summary>星尘辅助类</summary>
public static class StarHelper
{
    /// <summary>添加星尘</summary>
    /// <param name="services"></param>
    /// <param name="appId"></param>
    /// <returns></returns>
    public static StarFactory AddStardust(this IObjectContainer services, String appId) => AddStardust(services, null, appId, null);

    /// <summary>添加星尘</summary>
    /// <param name="services"></param>
    /// <param name="server">服务端地址。为空时先后读取appsettings.json、本地StarAgent、star.config，初始值为空，不连接服务端</param>
    /// <param name="appId">应用标识。为空时读取star.config，初始值为入口程序集名称</param>
    /// <param name="secret">应用密钥。为空时读取star.config，初始值为空</param>
    /// <returns></returns>
    public static StarFactory AddStardust(this IObjectContainer services, String? server = null, String? appId = null, String? secret = null)
    {
        var star = new StarFactory(server, appId, secret);

        if (services != ObjectContainer.Current && services is ObjectContainer container)
        {
            // 替换为混合配置提供者，优先本地配置
            var old = JsonConfigProvider.LoadAppSettings();
            star.SetLocalConfig(old);

            services.AddSingleton(star);
            services.AddSingleton(p => star.Tracer ?? DefaultTracer.Instance ?? (DefaultTracer.Instance ??= new DefaultTracer()));
            //services.AddSingleton(p => star.Config);
            services.AddSingleton(p => star.Service!);

            // 替换为混合配置提供者，优先本地配置
            services.AddSingleton(p => star.GetConfig()!);

            // 分布式缓存
            //services.TryAddSingleton<ICacheProvider, CacheProvider>();
            services.TryAddSingleton(XTrace.Log);
            services.TryAddSingleton(typeof(ICacheProvider), typeof(CacheProvider));
        }

        return star;
    }

    /// <summary>安全退出进程，目标进程还有机会执行退出代码</summary>
    /// <param name="process"></param>
    /// <param name="times"></param>
    /// <param name="sleep"></param>
    /// <returns></returns>
    public static Process? SafetyKill(this Process process, Int32 times = 50, Int32 sleep = 200)
    {
        if (process == null || process.GetHasExited()) return process;

        try
        {
            if (Runtime.Linux)
            {
                Process.Start("kill", process.Id.ToString()).WaitForExit(5_000);

                for (var i = 0; i < times && !process.GetHasExited(); i++)
                {
                    Thread.Sleep(sleep);
                }
            }
            else if (Runtime.Windows)
            {
                Process.Start("taskkill", $"-pid {process.Id}").WaitForExit(5_000);

                for (var i = 0; i < times && !process.GetHasExited(); i++)
                {
                    Thread.Sleep(sleep);
                }
            }
        }
        catch { }

        if (!process.GetHasExited()) process.Kill();

        return process;
    }

    /// <summary>强制结束进程树，包含子进程</summary>
    /// <param name="process"></param>
    /// <returns></returns>
    public static Process? ForceKill(this Process process)
    {
        if (process == null || process.GetHasExited()) return process;

        // 终止指定的进程及启动的子进程,如nginx等
        // 在Core 3.0, Core 3.1, 5, 6, 7, 8, 9 中支持此重载
        // https://learn.microsoft.com/zh-cn/dotnet/api/system.diagnostics.process.kill?view=net-8.0#system-diagnostics-process-kill(system-boolean)
#if NETCOREAPP
        process.Kill(true);
#else
        if (Runtime.Linux)
        {
            //-9 SIGKILL 强制终止信号
            Process.Start("kill", $"-9 {process.Id}").WaitForExit(5_000);
        }
        else if (Runtime.Windows)
        {
            // /f 指定强制终止进程，有子进程时只能强制
            // /t 终止指定的进程和由它启用的子进程 
            Process.Start("taskkill", $"/t /f /pid {process.Id}").WaitForExit(5_000);
        }

        if (!process.GetHasExited()) process.Kill();
#endif

        return process;
    }

    /// <summary>获取进程是否终止</summary>
    public static Boolean GetHasExited(this Process process)
    {
        try
        {
            return process.HasExited;
        }
        catch (Win32Exception)
        {
            return true;
        }
        //catch
        //{
        //    return false;
        //}
    }
}
