using System.Diagnostics;

Console.WriteLine("TestC启动，PID={0}", Process.GetCurrentProcess().Id);
Console.WriteLine("测试参数：{0}", String.Join(',', args));

Console.WriteLine("TestC OK!");
Console.ReadKey();