using NewLife.Agent;
using NewLife.Agent.Command;
using NewLife.Log;
using NewLife.Remoting;
using Stardust.Models;

namespace StarAgent.CommandHandler;

public class StartService : BaseCommandHandler
{
    public StartService(ServiceBase service) : base(service)
    {
        Cmd = "-StartService";
        Description = "启动子服务";
        ShortcutKey = '7';
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
        XTrace.WriteLine("Select the service to start: ");
        for (var i = 0; i < services.Services.Length; i++)
        {
            var svc = services.Services[i];
            XTrace.WriteLine("{0}. {1}", i + 1, svc.Name);
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
        
        XTrace.WriteLine("Preparing to start service: {0}", serviceName);

        try
        {
            // 通过 UDP RPC 启动服务
            var response = client.Invoke<ServiceOperationResult>("StartService", new { serviceName });

            if (response == null)
            {
                XTrace.WriteLine("Failed to start service: no response received");
                return;
            }

            if (response.Success)
            {
                XTrace.WriteLine("Service {0} started successfully", serviceName);
            }
            else
            {
                XTrace.WriteLine("Failed to start service: {0}", response.Message);
            }
        }
        catch (Exception ex)
        {
            XTrace.WriteLine("Error while starting service: {0}", ex.Message);
        }
    }
}

// 服务操作结果类，与API返回值对应
public class ServiceOperationResult
{
    public Boolean Success { get; set; }
    public String? Message { get; set; }
    public String? ServiceName { get; set; }
}