using NewLife.Agent;
using NewLife.Agent.Command;
using NewLife.Log;
using NewLife.Remoting;
using Stardust.Models;

namespace StarAgent.CommandHandler;

public class StopService : BaseCommandHandler
{
    public StopService(ServiceBase service) : base(service)
    {
        Cmd = "-StopService";
        Description = "停止子服务";
        ShortcutKey = '8';
    }

    public override void Process(String[] args)
    {
        var client = new ApiHttpClient("http://localhost:5500/");
        
        // 获取服务列表
        var services = client.Get<ServicesInfo>("GetServices");
        if (services == null || services.Services == null || services.Services.Length == 0)
        {
            XTrace.WriteLine("没有找到任何子服务");
            return;
        }
        
        // 显示服务列表
        XTrace.WriteLine("请选择要停止的服务：");
        for (var i = 0; i < services.Services.Length; i++)
        {
            var svc = services.Services[i];
            var es = services.RunningServices?.FirstOrDefault(e => e.Name == svc.Name);
            if (es != null) // 只显示正在运行的服务
            {
                XTrace.WriteLine("{0}. {1} (PID: {2})", i + 1, svc.Name, es.ProcessId);
            }
        }
        XTrace.WriteLine("0. 返回主菜单");
        
        // 获取用户输入
        XTrace.WriteLine("请输入服务序号或名称（0 返回主菜单）：");
        var input = Console.ReadLine();
        if (String.IsNullOrEmpty(input) || input == "0")
        {
            XTrace.WriteLine("返回主菜单...");
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
        
        XTrace.WriteLine("准备停止服务：{0}", serviceName);

        try
        {
            // 调用API停止服务
            var response = client.Get<ServiceOperationResult>("StopService", new { serviceName });

            if (response == null)
            {
                XTrace.WriteLine("停止服务失败：未收到响应");
                return;
            }

            if (response.Success)
            {
                XTrace.WriteLine("服务 {0} 停止成功", serviceName);
            }
            else
            {
                XTrace.WriteLine("停止服务失败：{0}", response.Message);
            }
        }
        catch (Exception ex)
        {
            XTrace.WriteLine("停止服务时发生错误：{0}", ex.Message);
        }
    }
}