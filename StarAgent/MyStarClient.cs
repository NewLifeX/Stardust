using System.Diagnostics;
using NewLife;
using NewLife.Agent;
using NewLife.Remoting.Clients;
using Stardust;

namespace StarAgent;

internal class MyStarClient : StarClient
{
    #region 属性
    //public IHost Host { get; set; }

    public ServiceBase Service { get; set; }
    #endregion

    #region 更新
    protected override void Restart(Upgrade upgrade)
    {
        // 带有-s参数就算是服务中运行
        var inService = "-s".EqualIgnoreCase(Environment.GetCommandLineArgs());
        var pid = Process.GetCurrentProcess().Id;

        // 以服务方式运行时，重启服务，否则采取拉起进程的方式
        if (inService || Service.Host is DefaultHost host && host.InService)
        {
            this.WriteInfoEvent("Upgrade", "强制更新完成，准备重启后台服务！PID=" + pid);

            //rs = Host.Restart("StarAgent");
            // 使用外部命令重启服务
            var rs = upgrade.Run("StarAgent", "-restart -upgrade");

            //!! 这里不需要自杀，外部命令重启服务会结束当前进程
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
}
