using NewLife;
using NewLife.Agent;
using NewLife.Agent.Command;
using NewLife.Log;
using NewLife.Remoting;
using Stardust.Models;

namespace StarAgent.CommandHandler;

public class RestartService : BaseCommandHandler
{
    public RestartService(ServiceBase service) : base(service)
    {
        Cmd = "-RestartService";
        Description = "重启子服务";
        ShortcutKey = '9';
    }

    public override void Process(String[] args)
    {
        var port = StarAgentSetting.Current.LocalPort;
        using var client = new ApiClient($"udp://127.0.0.1:{port}")
        {
            Timeout = 3_000,
        };
        
        // 获取服务列表
        var services = client.Invoke<ServicesInfo>("GetServices");
        if (services == null || services.Services == null || services.Services.Length == 0)
        {
            XTrace.WriteLine("No child services found");
            return;
        }
        
        // 显示服务列表
        XTrace.WriteLine("Select the service to restart: ");
        for (var i = 0; i < services.Services.Length; i++)
        {
            var svc = services.Services[i];
            var es = services.RunningServices?.FirstOrDefault(e => e.Name == svc.Name);
            var status = es != null ? L.T("Running") : L.T("Stopped");
            XTrace.WriteLine("{0}. {1} ({2})", i + 1, svc.Name, status);
        }
        XTrace.WriteLine("0. Return to the main menu");
        
        // 获取用户输入
        XTrace.WriteLine("Enter the service number or name (0 to return to the main menu): ");
        var input = Console.ReadLine();
        if (String.IsNullOrEmpty(input) || input == "0")
        {
            XTrace.WriteLine("Returning to the main menu...");
            return;
        }
        
        // 解析用户输入
        String serviceName;
        if (Int32.TryParse(input, out var index) && index > 0 && index <= services.Services.Length)
        {
            serviceName = services.Services[index - 1].Name;
        }
        else
        {
            serviceName = input;
        }
        
        XTrace.WriteLine("Preparing to restart service: {0}", serviceName);

        try
        {
            // 通过 UDP RPC 重启服务
            var response = client.Invoke<ServiceOperationResult>("RestartService", new { serviceName });

            if (response == null)
            {
                XTrace.WriteLine("Failed to restart service: no response received");
                return;
            }

            if (response.Success)
            {
                XTrace.WriteLine("Service {0} restarted successfully", serviceName);
            }
            else
            {
                XTrace.WriteLine("Failed to restart service: {0}", response.Message);
            }
        }
        catch (Exception ex)
        {
            XTrace.WriteLine("Error while restarting service: {0}", ex.Message);
        }
    }
}