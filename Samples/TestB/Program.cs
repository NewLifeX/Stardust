using System.Diagnostics;
using NewLife;
using NewLife.Log;

XTrace.UseConsole();

XTrace.WriteLine("TestB启动，PID={0}", Process.GetCurrentProcess().Id);
XTrace.WriteLine("测试参数：{0}", args.Join(" "));

XTrace.WriteLine("GetFullPath：\t{0}", Path.GetFullPath("."));
XTrace.WriteLine("CurrentDirectory：\t{0}", Environment.CurrentDirectory);
XTrace.WriteLine("BaseDirectory：\t{0}", AppDomain.CurrentDomain.BaseDirectory);
XTrace.WriteLine("BasePath：\t{0}", PathHelper.BasePath);
XTrace.WriteLine("BaseDirectory：\t{0}", PathHelper.BaseDirectory);

var envs = new[] { "BasePath", "star" };
XTrace.WriteLine("环境变量：");
var dic = Runtime.GetEnvironmentVariables().OrderBy(e => e.Key).ToDictionary(e => e.Key, e => e.Value);
foreach (var item in dic)
{
    if (item.Key.EqualIgnoreCase(envs))
        XTrace.WriteLine("{0}:\t{1}", item.Key, item.Value);
}

Console.WriteLine("TestB OK!");
//Console.ReadKey();
Thread.Sleep(15_000);
XTrace.WriteLine("Auto Exist!");