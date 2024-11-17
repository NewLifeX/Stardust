using System.Diagnostics;
using NewLife;
using NewLife.Agent;
using NewLife.Log;
using NewLife.Remoting.Clients;
using NewLife.Remoting.Models;
using NewLife.Serialization;
using Stardust;
using Stardust.Models;

namespace StarAgent;

internal class MyStarClient : StarClient
{
    #region 属性
    //public IHost Host { get; set; }

    public ServiceBase Service { get; set; }

    public StarAgentSetting AgentSetting { get; set; }

    ///// <summary>项目名。新节点默认所需要加入的项目</summary>
    //public String Project { get; set; }

    private Boolean InService
    {
        get
        {
            var inService = "-s".EqualIgnoreCase(Environment.GetCommandLineArgs());
            if (inService) return true;

            // 以服务方式运行时，重启服务，否则采取拉起进程的方式
            if (Service != null && Service.Host is DefaultHost host && host.InService) return true;

            return false;
        }
    }
    #endregion

    #region 登录
    public override void Open()
    {
        this.RegisterCommand("node/restart", Restart);
        this.RegisterCommand("node/reboot", Reboot);
        this.RegisterCommand("node/setchannel", SetChannel);

        base.Open();
    }

    public override ILoginRequest BuildLoginRequest()
    {
        var set = AgentSetting;
        var request = base.BuildLoginRequest();
        if (request is LoginInfo req)
        {
            req.Project = set.Project;

            var info = req.Node;
            if (info != null && InService)
            {
                if (!set.Dpi.IsNullOrEmpty()) info.Dpi = set.Dpi;
                if (!set.Resolution.IsNullOrEmpty()) info.Resolution = set.Resolution;
            }
        }

        return request;
    }
    #endregion

    #region 更新
    public override Task<IUpgradeInfo> Upgrade(String channel, CancellationToken cancellationToken = default)
    {
        if (channel.IsNullOrEmpty()) channel = AgentSetting.Channel;

        return base.Upgrade(channel, cancellationToken);
    }

    protected override void Restart(Upgrade upgrade)
    {
        // 带有-s参数就算是服务中运行
        var inService = "-s".EqualIgnoreCase(Environment.GetCommandLineArgs());
        var pid = Process.GetCurrentProcess().Id;

        // 以服务方式运行时，重启服务，否则采取拉起进程的方式
        if (inService || Service.Host is DefaultHost host && host.InService)
        {
            this.WriteInfoEvent("Upgrade", "强制更新完成，准备重启后台服务！PID=" + pid);

            // 使用外部命令重启服务
            var rs = upgrade.Run("StarAgent", "-restart -upgrade");

            //!! 这里不需要自杀，外部命令重启服务会结束当前进程
            if (rs)
            {
                this.WriteInfoEvent("Upgrade", "强制更新完成，新进程已拉起，等待当前服务被重启！");
            }
            else
            {
                this.WriteInfoEvent("Upgrade", "强制更新完成，但拉起新进程失败");
            }
        }
        else
        {
            // 重新拉起进程
            var rs = upgrade.Run("StarAgent", "-run -upgrade");
            if (rs)
            {
                Service.StopWork("Upgrade");

                this.WriteInfoEvent("Upgrade", "强制更新完成，新进程已拉起，准备退出当前进程！PID=" + pid);

                upgrade.KillSelf();
            }
            else
            {
                this.WriteInfoEvent("Upgrade", "强制更新完成，但拉起新进程失败");
            }
        }
    }
    #endregion

    #region 扩展功能
    /// <summary>重启应用服务</summary>
    private String Restart(String argument)
    {
        // 异步执行，让方法调用返回结果给服务端
        Task.Factory.StartNew(() =>
        {
            Thread.Sleep(1000);

            var upgrade = new Upgrade { Log = XTrace.Log };

            // 带有-s参数就算是服务中运行
            var inService = "-s".EqualIgnoreCase(Environment.GetCommandLineArgs());

            // 以服务方式运行时，重启服务，否则采取拉起进程的方式
            if (inService || Service.Host is DefaultHost host && host.InService)
            {
                // 使用外部命令重启服务
                var rs = upgrade.Run("StarAgent", "-restart -delay");

                //!! 这里不需要自杀，外部命令重启服务会结束当前进程
                return rs + "";
            }
            else
            {
                // 重新拉起进程
                var rs = upgrade.Run("StarAgent", "-run -delay");
                if (rs)
                {
                    Service.StopWork("Upgrade");

                    upgrade.KillSelf();
                }

                return rs + "";
            }
        });

        return "success";
    }

    /// <summary>重启操作系统</summary>
    private String Reboot(String argument)
    {
        var dic = argument.IsNullOrEmpty() ? null : JsonParser.Decode(argument);
        var timeout = dic?["timeout"].ToInt();

        // 异步执行，让方法调用返回结果给服务端
        Task.Factory.StartNew(() =>
        {
            Thread.Sleep(1000);

            if (Runtime.Windows)
            {
                if (timeout > 0)
                    "shutdown".ShellExecute($"-r -t {timeout}");
                else
                    "shutdown".ShellExecute($"-r");

                Thread.Sleep(5000);
                "shutdown".ShellExecute($"-r -f");
            }
            else if (Runtime.Linux)
            {
                // 多种方式重启Linux，先使用温和的方式
                "systemctl".ShellExecute("reboot");

                Thread.Sleep(5000);
                "shutdown".ShellExecute("-r now");

                Thread.Sleep(5000);
                "reboot".ShellExecute();
            }
        });

        return "success";
    }

    /// <summary>设置通道</summary>
    /// <param name="argument"></param>
    /// <returns></returns>
    private String SetChannel(String argument)
    {
        if (argument.IsNullOrEmpty()) return "参数为空";

        var set = AgentSetting;
        set.Channel = argument;
        set.Save();

        return "success " + argument;
    }
    #endregion
}
