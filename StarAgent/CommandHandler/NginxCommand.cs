using NewLife.Agent;
using NewLife.Agent.Command;
using NewLife.Log;
using StarAgent.Managers;

namespace StarAgent.CommandHandler;

internal class NgidnxCommand : BaseCommandHandler
{
    public NgidnxCommand(ServiceBase service) : base(service)
    {
        Cmd = "-nginx";
        Description = "配置Nginx";
        ShortcutKey = 'n';
    }

    public override void Process(String[] args)
    {
        var dir = "./Config/";
        var sites = NginxDeploy.DetectNginxConfig(dir).ToList();
        if (sites.Count == 0)
        {
            XTrace.WriteLine("没有找到Nginx配置文件，请检查目录 {0} 是否存在", dir);
            return;
        }

        XTrace.WriteLine("Nginx配置目录：{0}", sites[0].ConfigPath);
        XTrace.WriteLine("Nginx扩展名：{0}", sites[0].Extension);

        foreach (var nd in sites)
        {
            XTrace.WriteLine("站点：{0}", nd.SiteFile);
            nd.Publish();
        }
    }
}
