using NewLife;
using NewLife.Model;
using NewLife.Remoting.Clients;
using NewLife.Serialization;
using Stardust;
using Stardust.Models;

namespace DeployAgent;

public class DeployWorker : IHostedService
{
    private readonly StarFactory _factory;

    public DeployWorker(StarFactory factory)
    {
        _factory = factory;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _factory.App.RegisterCommand("deploy/compile", OnCompile);

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
