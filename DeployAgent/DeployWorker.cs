using NewLife;
using NewLife.Log;
using NewLife.Model;
using NewLife.Remoting.Clients;
using NewLife.Serialization;
using Stardust;
using Stardust.Models;

namespace DeployAgent;

public class DeployWorker(StarFactory factory) : IHostedService
{
    private StarClient _client;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        XTrace.WriteLine("开始Deploy客户端");

        var set = DeploySetting.Current;

        // 产品编码、产品密钥从IoT管理平台获取，设备编码支持自动注册
        var client = new StarClient(factory.Server)
        {
            Name = "Deploy",
            Code = set.Code,
            Secret = set.Secret,
            ProductCode = "StarDeploy",
            Setting = set,

            Tracer = factory.Tracer,
            Log = XTrace.Log,
        };

        // 禁用客户端特性
        client.Features &= ~Features.Upgrade;

        client.Open();

        Host.RegisterExit(() => client.Logout("ApplicationExit"));

        _client = client;

        client.RegisterCommand("deploy/compile", OnCompile);

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _client.TryDispose();

        return Task.CompletedTask;
    }

    private String OnCompile(String args)
    {
        if (args.IsNullOrEmpty()) throw new ArgumentNullException(nameof(args));

        var cmd = args.ToJsonEntity<CompileCommand>();
        if (cmd == null || cmd.Repository.IsNullOrEmpty()) throw new ArgumentNullException(nameof(cmd.Repository));

        //todo 拉取代码编译逻辑

        return "OK";
    }
}
