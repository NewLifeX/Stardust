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
        var client = new ApiHttpClient("http://localhost:5500/");

        try
        {
            // 调用GET请求。
            var response = client.Get<ServicesInfo>("GetServices");

            if (response == null || response.Services == null)
            {
                XTrace.WriteLine("没有找到任何子服务");
                return;
            }

            XTrace.WriteLine("所有子服务列表：");
            XTrace.WriteLine("{0,-5} {1,-15} {2,-5} {3,-10} {4,-10} {5,-10}", "序号", "服务名称", "启用", "状态", "进程Id", "进程名称");

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
        //    Console.WriteLine("没有找到任何子服务");
        //    return;
        //}

        //Console.WriteLine("所有子服务列表：");
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