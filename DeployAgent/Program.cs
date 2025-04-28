using System.Reflection;
using DeployAgent;
using DeployAgent.Commands;
using NewLife;
using NewLife.Log;
using NewLife.Model;
using Stardust;

// 启用控制台日志，拦截所有异常
XTrace.UseConsole();

// 初始化对象容器，提供注入能力
var services = ObjectContainer.Current;
//services.AddSingleton(XTrace.Log);

//var asm = Assembly.GetEntryAssembly();
//Console.WriteLine("星尘发布 StarDeploy v{0}", asm.GetName().Version);
//Console.WriteLine(asm.GetCustomAttribute<AssemblyDescriptionAttribute>()?.Description);
//Console.WriteLine("{0}", Environment.OSVersion);
//Console.WriteLine();

var cmd = args?.FirstOrDefault();
if (args != null && !cmd.IsNullOrEmpty())
{
    var cmds = new Dictionary<String, ICommand>
    {
        { "pack", new PackCommand() },
        { "deploy", new DeployCommand() }
    };

    if (cmds.TryGetValue(cmd, out var command))
    {
        // 执行命令
        command.Process(args.Skip(1).ToArray());
        return;
    }
}

// 没有命令时走默认逻辑
if (cmd.IsNullOrEmpty())
{
    // 配置星尘。自动读取配置文件 config/star.config 中的服务器地址
    var star = services.AddStardust();

    services.AddHostedService<DeployWorker>();

    var host = services.BuildHost();

    // 异步阻塞，友好退出
    await host.RunAsync();
}