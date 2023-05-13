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
        vers.AddRange(NetRuntime.Get1To45VersionFromRegistry());
        vers.AddRange(NetRuntime.Get45PlusFromRegistry());
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
        if (model.BaseUrl.IsNullOrEmpty()) nr.BaseUrl = model.BaseUrl;

        // 获取已安装版本集合
        var ver = model.Version.Trim('v', 'V');
        if (ver.StartsWithIgnoreCase("2.", "3.5", "4.0", "4.5"))
        {
        }
        else if (ver.StartsWithIgnoreCase("4."))
        {
        }
        else if (ver.StartsWithIgnoreCase("6."))
        {
        }

        return "安装成功";
    }

    private String DoUninstall(String argument)
    {
        var model = argument.ToJsonEntity<FrameworkModel>();
        if (model == null || model.Version.IsNullOrEmpty()) throw new Exception("未指定版本！");

        return "卸载成功";
    }
}
