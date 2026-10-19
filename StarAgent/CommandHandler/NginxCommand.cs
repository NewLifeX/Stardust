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
            XTrace.WriteLine("No nginx configuration file found; check whether directory {0} exists", dir);
            return;
        }

        XTrace.WriteLine("Nginx configuration directory: {0}", sites[0].ConfigPath);
        XTrace.WriteLine("Nginx extension: {0}", sites[0].Extension);

        foreach (var nd in sites)
        {
            XTrace.WriteLine("Site: {0}", nd.SiteFile);
            nd.Publish();
        }
    }
}
