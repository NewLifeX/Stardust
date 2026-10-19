using NewLife.Agent.Command;
using NewLife.Agent;
using NewLife;
using NewLife.Serialization;

namespace StarAgent.CommandHandler;

public class UseMicroService : BaseCommandHandler
{
    public UseMicroService(ServiceBase service) : base(service)
    {
        Cmd = "-UseMicroService";
        Description = "测试微服务";
        ShortcutKey = 'w';
    }

    private String _lastService;

    public override void Process(String[] args)
    {
        var service = (MyService)Service;
        if (_lastService.IsNullOrEmpty())
            Console.WriteLine(L.T("Enter the microservice name to test: "));
        else
            Console.WriteLine(L.T("Enter the microservice name to test ({0}): "), _lastService);

        var serviceName = Console.ReadLine();
        if (serviceName.IsNullOrEmpty()) serviceName = _lastService;
        if (serviceName.IsNullOrEmpty()) return;

        _lastService = serviceName;

        service.StartFactory();

        var models = service._factory.Service.ResolveAsync(serviceName).ConfigureAwait(false).GetAwaiter().GetResult();

        Console.WriteLine(models.ToJson(true));
    }
}
