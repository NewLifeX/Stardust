using System.Collections;
using System.Diagnostics;

Console.WriteLine("TestC启动，PID={0}", Process.GetCurrentProcess().Id);
Console.WriteLine("测试参数：{0}", String.Join(',', args));

Console.WriteLine("GetFullPath：\t{0}", Path.GetFullPath("."));
Console.WriteLine("CurrentDirectory：\t{0}", Environment.CurrentDirectory);
Console.WriteLine("BaseDirectory：\t{0}", AppDomain.CurrentDomain.BaseDirectory);
Console.WriteLine("BasePath：\t{0}", Environment.GetEnvironmentVariable("BasePath"));

var envs = new[] { "BasePath", "star" };
Console.WriteLine("环境变量：");
foreach (DictionaryEntry item in Environment.GetEnvironmentVariables())
{
    if (envs.Contains(item.Key))
        Console.WriteLine("{0}:\t{1}", item.Key, item.Value);
}

Console.WriteLine("TestC OK!");
//Console.ReadKey();
Thread.Sleep(15_000);
Console.WriteLine("Auto Exist!");