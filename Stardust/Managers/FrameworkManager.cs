using NewLife;
using NewLife.Log;
using NewLife.Remoting.Clients;
using NewLife.Serialization;
using Stardust.Models;
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
            Force = model.Force,
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
        else if (ver.StartsWithIgnoreCase("9."))
        {
            var kind = "";
            var p = ver.IndexOf('-');
            if (p > 0)
            {
                kind = ver.Substring(p + 1);
                ver = ver.Substring(0, p);
            }

            nr.InstallNet9(ver, kind);
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

        //目前仅支持NetCore
        var ver = model.Version.Trim('v', 'V');
        var deleted = false;
        var versions = NetRuntime.GetNetCore(false);

        foreach (var version in versions)
        {
            //删除指定
            var currentVer = version.Name.TrimStart('v', 'V');
            if (ver != currentVer) { continue; }

            if (Runtime.Linux)
            {
                var runtimes = new String[] { "Microsoft.NETCore.App", "Microsoft.AspNetCore.App" };
                var rootDir = "/usr/share/dotnet/shared";
                foreach (var runtime in runtimes)
                {
                    var dir = rootDir.CombinePath(runtime, currentVer);
                    if (Directory.Exists(dir))
                    {
                        //有可能被占用
                        try
                        {
                            Directory.Delete(dir, true);
                            deleted = true;
                            XTrace.Log.Info($"{runtime} {currentVer} 已删除");
                        }
                        catch (Exception ex)
                        {
                            XTrace.Log.Info($"卸载时出现异常 {ex.Message}");
                        }
                    }
                }
            }
            else if (Runtime.Windows)
            {
                var runtimes = new String[] { "Microsoft.NETCore.App", "Microsoft.AspNetCore.App", "Microsoft.WindowsDesktop.App" };
                var rootDir = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles).CombinePath("dotnet", "shared");
                foreach (var runtime in runtimes)
                {
                    var dir = rootDir.CombinePath(runtime, currentVer);
                    if (Directory.Exists(dir))
                    {
                        try
                        {
                            Directory.Delete(dir, true);
                            deleted = true;
                            XTrace.Log.Info($"{runtime} {currentVer} 已删除");
                        }
                        catch (Exception ex)
                        {
                            XTrace.Log.Info($"卸载时出现异常 {ex.Message}");
                        }                        
                    }
                }
            }
            else
            {
                XTrace.Log.Info("暂不支持当前OS卸载");
                throw new Exception("暂不支持当前OS卸载");
            }
        }

        XTrace.Log.Info("{0} 卸载成功", model.Version);
        CheckPing();

        return deleted ? "卸载成功" : "卸载失败";
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
