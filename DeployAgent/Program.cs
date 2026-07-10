using System.Reflection;
using DeployAgent;
using DeployAgent.Commands;
using NewLife;
using NewLife.Log;

namespace DeployAgent;

internal class Program
{
    private static void Main(String[] args)
    {
        // 启用控制台日志，拦截所有异常
        XTrace.UseConsole();

        var asm = Assembly.GetEntryAssembly();
        Console.WriteLine("星尘发布 \e[31;1mstardeploy\e[0m v{0}", asm.GetName().Version);
        Console.WriteLine(asm.GetCustomAttribute<AssemblyDescriptionAttribute>()?.Description);
        Console.WriteLine("\e[34;1m{0}\e[0m", Environment.OSVersion);
        Console.WriteLine();

        var cmds = new Dictionary<String, ICommand>
        {
            { "pack", new PackCommand() },
            { "deploy", new DeployCommand() }
        };
        Console.WriteLine("可用命令：");

        foreach (var item in cmds)
        {
            var type = item.Value.GetType();

            Console.WriteLine("\t{0,-8}\t{1}", item.Key, type.GetDescription() ?? type.FullName);
        }

        var cmd = args?.FirstOrDefault();
        if (args != null && !cmd.IsNullOrEmpty())
        {
            if (cmds.TryGetValue(cmd, out var command))
            {
                try
                {
                    // 执行命令
                    command.Process(args.Skip(1).ToArray());
                    return;
                }
                catch (Exception ex)
                {
                    XTrace.WriteException(ex);
                    Thread.Sleep(15_000);
                }
            }
        }

        // 没有命令时，进入服务模式（支持 -install/-uninstall/-start/-stop/-run/-s 等）
        if (cmd.IsNullOrEmpty())
        {
            var svc = new DeployService
            {
                Log = XTrace.Log,
            };

            svc.Main(args);
        }
    }
}
