using NewLife;
using NewLife.Log;
using NewLife.Serialization;
using Stardust.Models;
using Stardust.Services;
#if NET45_OR_GREATER || NETCOREAPP || NETSTANDARD
using TaskEx = System.Threading.Tasks.Task;
#endif

namespace Stardust.Managers;

/// <summary>框架管理器</summary>
public class FrameworkManager
{
    private IEventProvider? _eventProvider;

    /// <summary>获取所有框架版本</summary>
    /// <returns></returns>
    public String[] GetAllVersions()
    {
        var vers = new List<VerInfo>();
#if NET5_0_OR_GREATER
        if (OperatingSystem.IsWindows())
        {
            vers.AddRange(NetRuntime.Get1To45VersionFromRegistry());
            vers.AddRange(NetRuntime.Get45PlusFromRegistry());
        }
#else
        vers.AddRange(NetRuntime.Get1To45VersionFromRegistry());
        vers.AddRange(NetRuntime.Get45PlusFromRegistry());
#endif
        vers.AddRange(NetRuntime.GetNetCore(false));

        //return vers.Join(",", e => e.Name.TrimStart('v'));
        return vers.Select(e => e.Name).ToArray();
    }

    /// <summary>附加刷新命令</summary>
    /// <param name="client"></param>
    public void Attach(ICommandClient client)
    {
        _eventProvider = client as IEventProvider;

        client.RegisterCommand("framework/install", DoInstall);
        client.RegisterCommand("framework/uninstall", DoUninstall);
    }

    private String? DoInstall(String? argument)
    {
        var model = argument?.ToJsonEntity<FrameworkModel>();
        if (model == null || model.Version.IsNullOrEmpty()) throw new Exception("未指定版本！");

        var nr = new NetRuntime
        {
            Silent = true,
            EventProvider = _eventProvider,
            Log = XTrace.Log,
        };
        if (!model.BaseUrl.IsNullOrEmpty()) nr.BaseUrl = model.BaseUrl;

        // 获取已安装版本集合
        var ver = model.Version.Trim('v', 'V');
        if (Runtime.Linux)
        {
            var kind = "";
            var p = ver.IndexOf('-');
            if (p > 0)
            {
                kind = ver.Substring(p + 1);
                ver = ver.Substring(0, p);
            }

            nr.InstallNetOnLinux(ver, kind);
        }
        else if (ver.StartsWithIgnoreCase("4.0"))
        {
#if NET5_0_OR_GREATER
            if (OperatingSystem.IsWindows())
                nr.InstallNet40();
#else
            nr.InstallNet40();
#endif
        }
        else if (ver.StartsWithIgnoreCase("4.5"))
        {
#if NET5_0_OR_GREATER
            if (OperatingSystem.IsWindows())
                nr.InstallNet45();
#else
            nr.InstallNet45();
#endif
        }
        else if (ver.StartsWithIgnoreCase("4."))
        {
#if NET5_0_OR_GREATER
            if (OperatingSystem.IsWindows())
                nr.InstallNet48();
#else
            nr.InstallNet48();
#endif
        }
        else if (ver.StartsWithIgnoreCase("6."))
        {
            var kind = "";
            var p = ver.IndexOf('-');
            if (p > 0)
            {
                kind = ver.Substring(p + 1);
                ver = ver.Substring(0, p);
            }

            nr.InstallNet6(ver, kind);
        }
        else if (ver.StartsWithIgnoreCase("7."))
        {
            var kind = "";
            var p = ver.IndexOf('-');
            if (p > 0)
            {
                kind = ver.Substring(p + 1);
                ver = ver.Substring(0, p);
            }

            nr.InstallNet7(ver, kind);
        }
        else if (ver.StartsWithIgnoreCase("8."))
        {
            var kind = "";
            var p = ver.IndexOf('-');
            if (p > 0)
            {
                kind = ver.Substring(p + 1);
                ver = ver.Substring(0, p);
            }

            nr.InstallNet8(ver, kind);
        }
        else
            throw new Exception($"不支持的.NET版本[{ver}]");

        CheckPing();

        return "安装成功";
    }

    private String? DoUninstall(String? argument)
    {
        var model = argument?.ToJsonEntity<FrameworkModel>();
        if (model == null || model.Version.IsNullOrEmpty()) throw new Exception("未指定版本！");

        CheckPing();

        return "卸载成功";
    }

    /// <summary>星尘安装卸载框架后，马上执行一次心跳，使得其尽快上报框架版本</summary>
    void CheckPing()
    {
        if (_eventProvider is StarClient client)
        {
            TaskEx.Run(async () =>
            {
                await client.Ping();
                await TaskEx.Delay(1000);

                //!! 要执行整个升级动作，而不仅仅是拉取新版本
                //await client.Upgrade("", "");
                await client.SendCommand("node/upgrade", "");
            });
        }
    }
}
