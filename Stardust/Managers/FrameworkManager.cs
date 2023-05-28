using NewLife;
using NewLife.Serialization;
using Stardust.Models;
using Stardust.Services;

namespace Stardust.Managers;

/// <summary>框架管理器</summary>
public class FrameworkManager
{
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
        client.RegisterCommand("framework/install", DoInstall);
        client.RegisterCommand("framework/uninstall", DoUninstall);
    }

    private String DoInstall(String argument)
    {
        var model = argument.ToJsonEntity<FrameworkModel>();
        if (model == null || model.Version.IsNullOrEmpty()) throw new Exception("未指定版本！");

        var nr = new NetRuntime
        {
            Silent = true
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
#if NET5_0_OR_GREATER
        else if (OperatingSystem.IsWindows())
        {
            if (ver.StartsWithIgnoreCase("4.0"))
            {
                nr.InstallNet40();
            }
            else if (ver.StartsWithIgnoreCase("4.5"))
            {
                nr.InstallNet45();
            }
            else if (ver.StartsWithIgnoreCase("4."))
            {
                nr.InstallNet48();
            }
        }
#else
        else if (ver.StartsWithIgnoreCase("4.0"))
        {
            nr.InstallNet40();
        }
        else if (ver.StartsWithIgnoreCase("4.5"))
        {
            nr.InstallNet45();
        }
        else if (ver.StartsWithIgnoreCase("4."))
        {
            nr.InstallNet48();
        }
#endif
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

            nr.InstallNet7(ver, kind);
        }
        else
            throw new Exception($"不支持的.NET版本[{ver}]");

        return "安装成功";
    }

    private String DoUninstall(String argument)
    {
        var model = argument.ToJsonEntity<FrameworkModel>();
        if (model == null || model.Version.IsNullOrEmpty()) throw new Exception("未指定版本！");

        return "卸载成功";
    }
}
