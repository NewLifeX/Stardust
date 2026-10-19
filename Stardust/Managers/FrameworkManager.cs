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
        if (model == null || model.Version.IsNullOrEmpty()) throw new Exception(L.T("No version was specified."));

        WriteLog(L.TLog("Installing {0}", model.Version));

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
        else if (Runtime.OSX)
        {
            var kind = "";
            var p = ver.IndexOf('-');
            if (p > 0)
            {
                kind = ver.Substring(p + 1);
                ver = ver.Substring(0, p);
            }

            nr.InstallNetOnOSX(ver, kind);
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
        else
        {
            // 支持标准dotNet版本安装
            var pv = ver.IndexOf('.');
            if (pv > 0 && ver.Substring(0, pv).ToInt() >= 5)
            {
                var kind = "";
                var p = ver.IndexOf('-');
                if (p > 0)
                {
                    kind = ver.Substring(p + 1);
                    ver = ver.Substring(0, p);
                }

                nr.InstallNet("v" + ver.Substring(0, pv), ver, kind);
            }
            else
                throw new Exception(L.T("Unsupported .NET version [{0}]", ver));
        }

        CheckPing();

        return "安装成功";
    }

    private String? DoUninstall(String? argument)
    {
        var model = argument?.ToJsonEntity<FrameworkModel>();
        if (model == null || model.Version.IsNullOrEmpty()) throw new Exception(L.T("No version was specified."));

        var ver = model.Version;
        WriteLog(L.TLog("Uninstalling {0}", ver));

        //目前仅支持NetCore
        var rs = false;
        if (ver.Contains('*'))
        {
            var versions = NetRuntime.GetNetCore(false);
            foreach (var item in versions)
            {
                if (ver.IsMatch(item.Name))
                    rs |= Uninstall(item.Name);
            }
        }
        else
        {
            rs |= Uninstall(ver);
        }

        CheckPing();

        return rs ? L.T("Uninstalled successfully") : L.T("Uninstall failed");
    }

    Boolean Uninstall(String verion)
    {
        WriteLog(L.TLog("Uninstalling {0}", verion));

        var ver = verion.Trim('v', 'V');
        var p = ver.IndexOf('-');
        if (p > 0) ver = ver.Substring(0, p);

        var rs = false;
        var rootDir = "";
        if (Runtime.Linux)
        {
            rootDir = "/usr/share/dotnet/";
        }
        else if (Runtime.Windows)
        {
            rootDir = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles).CombinePath("dotnet");
        }
        else
        {
            WriteLog(L.TLog("Uninstalling is not supported on the current OS."));
            throw new Exception(L.T("Uninstalling is not supported on the current OS."));
        }

        var paths = new String[] { "host/fxr", "shared/Microsoft.NETCore.App", "shared/Microsoft.AspNetCore.App", "shared/Microsoft.WindowsDesktop.App" };
        foreach (var item in paths)
        {
            var dir = rootDir.CombinePath(item, ver);
            if (Directory.Exists(dir))
            {
                //有可能被占用
                try
                {
                    Directory.Delete(dir, true);
                    rs = true;
                    WriteLog(L.TLog("Deleting {0}/{1}", item, ver));
                }
                catch (Exception ex)
                {
                    WriteLog(L.TLog("Error while uninstalling: {0}", ex.Message));
                }
            }
        }

        if (rs)
            WriteLog("{0} uninstalled successfully", verion);
        else
            WriteLog("Failed to uninstall {0}", verion);

        return rs;
    }

    /// <summary>星尘安装卸载框架后，马上执行一次心跳，使得其尽快上报框架版本</summary>
    void CheckPing()
    {
        if (_eventProvider is StarClient client)
        {
            TaskEx.Run(async () =>
            {
                await client.Ping().ConfigureAwait(false);
                await TaskEx.Delay(1000).ConfigureAwait(false);

                //!! 要执行整个升级动作，而不仅仅是拉取新版本
                //await client.Upgrade("", "");
                await client.SendCommand("node/upgrade", "").ConfigureAwait(false);
            });
        }
    }

    #region 日志
    /// <summary>日志</summary>
    public ILog Log { get; set; } = XTrace.Log;

    /// <summary>写日志</summary>
    /// <param name="format"></param>
    /// <param name="args"></param>
    public void WriteLog(String format, params Object?[] args)
    {
        Log?.InfoPrefixed("[FrameworkManager]", format, args);

        var msg = (args == null || args.Length == 0) ? format : String.Format(format, args);
        DefaultSpan.Current?.AppendTag(msg);

        if (format.Contains("错误") || format.Contains("失败") || format.Contains("error", StringComparison.OrdinalIgnoreCase) || format.Contains("fail", StringComparison.OrdinalIgnoreCase) || format.Contains("exception", StringComparison.OrdinalIgnoreCase))
            _eventProvider?.WriteErrorEvent(nameof(FrameworkManager), msg);
        else
            _eventProvider?.WriteInfoEvent(nameof(FrameworkManager), msg);
    }
    #endregion
}