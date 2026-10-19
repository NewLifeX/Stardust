using Stardust.Models;
using NewLife.Agent;
using NewLife.Agent.Command;
using NewLife.Log;
using NewLife.Remoting;

namespace StarAgent.CommandHandler;

public class ListServices : BaseCommandHandler
{
    public ListServices(ServiceBase service) : base(service)
    {
        Cmd = "-ListServices";
        Description = "查看子服务";
        ShortcutKey = '6';
    }

    public override void Process(String[] args)
    {
        var port = StarAgentSetting.Current.LocalPort;
        using var client = new ApiClient($"udp://127.0.0.1:{port}")
        {
            Timeout = 3_000,
        };

        try
        {
            // 通过 UDP RPC 获取服务列表
            var response = client.Invoke<ServicesInfo>("GetServices");

            if (response == null || response.Services == null)
            {
                XTrace.WriteLine("No child services found");
                return;
            }

            XTrace.WriteLine("All child services:");
            XTrace.WriteLine("{0,-5} {1,-15} {2,-5} {3,-10} {4,-10} {5,-10}", "No.", "Service name", "Enabled", "Status", "Process ID", "Process name");

            for (var i = 0; i < response.Services.Length; i++)
            {
                var svc = response.Services[i];

                var es = response.RunningServices?.FirstOrDefault(e => e.Name == svc.Name);

                XTrace.WriteLine("{0,-7} {1,-20} {2,-5} {3,-10} {4,-10} {5,-10}",
                    i + 1,
                    svc.Name,
                    svc.Enable ? "是" : "否",
                    es != null ? "启动中" : "停止",
                    es?.ProcessId,
                    es?.ProcessName);
            }

            //// 处理响应数据。
            //Console.WriteLine($"Response: {response?.ToJson()}");
        }
        catch (Exception ex)
        {
            // 错误处理。
            Console.WriteLine($"Error: {ex.Message}");
        }

        //var service = (MyService)Service;
        //var manager = service.Provider?.GetService(typeof(ServiceManager)) as ServiceManager;

        //if (manager == null || manager.Services == null || manager.Services.Length == 0)
        //{
        //    Console.WriteLine(L.T("No child services found"));
        //    return;
        //}

        //Console.WriteLine(L.T("All child services:"));
        //Console.WriteLine("序号\t服务名称\t启用");

        //for (int i = 0; i < manager.Services.Length; i++)
        //{
        //    var svc = manager.Services[i];

        //    Console.WriteLine("{0}\t{1}\t{2}",
        //        i + 1,
        //        svc.Name,
        //        svc.Enable ? "是" : "否");
        //}
    }
}