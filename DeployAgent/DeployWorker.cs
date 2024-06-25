using NewLife;
using NewLife.Model;
using NewLife.Remoting.Clients;
using NewLife.Serialization;
using Stardust;
using Stardust.Models;

namespace DeployAgent;

public class DeployWorker : IHostedService
{
    private readonly AppClient _appClient;

    public DeployWorker(AppClient appClient)
    {
        _appClient = appClient;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _appClient.RegisterCommand("deploy/compile", OnCompile);

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private String OnCompile(String args)
    {
        if (args.IsNullOrEmpty()) throw new ArgumentNullException(nameof(args));

        var cmd = args.ToJsonEntity<CompileCommand>();
        if (cmd == null || cmd.Repository.IsNullOrEmpty()) throw new ArgumentNullException(nameof(cmd.Repository));

        //todo 拉取代码编译逻辑

        return "OK";
    }
}
