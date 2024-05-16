using System.Diagnostics;
using NewLife;
using NewLife.Log;

XTrace.UseConsole();

XTrace.WriteLine("TestA启动，PID={0}", Process.GetCurrentProcess().Id);
XTrace.WriteLine("测试参数：{0}", args.Join(" "));

var target = "TestB";
if (args.Contains("-c")) target = "TestC";

var old = Process.GetProcesses().FirstOrDefault(e => e.ProcessName == target);
if (old != null)
{
    XTrace.WriteLine("关闭进程 {0} {1}", old.Id, old.ProcessName);
    old.Kill();
}

var si = new ProcessStartInfo
{
    FileName = (Runtime.Windows ? $"../{target}/{target}.exe" : $"../{target}/{target}").GetFullPath(),
    Arguments = "-name NewLife",
    //WorkingDirectory = "",
    //UseShellExecute = false,
};

// 必须在si.Environment之前设置，否则丢失。可能si.Environment复制了一份
if (args.Contains("-b")) Environment.SetEnvironmentVariable("BasePath", $"../{target}".GetFullPath());
if (args.Contains("-s")) si.UseShellExecute = true;
if (args.Contains("-w")) si.WorkingDirectory = Path.GetDirectoryName(si.FileName)?.GetFullPath();
if (args.Contains("-e")) si.Environment["star"] = "dust";

XTrace.WriteLine("UseShellExecute:\t{0}", si.UseShellExecute);
XTrace.WriteLine("WorkingDirectory:\t{0}", si.WorkingDirectory);

var p = Process.Start(si);
if (p == null || p.WaitForExit(3_000) && p.ExitCode != 0)
{
    XTrace.WriteLine("启动失败！ExitCode={0}", p?.ExitCode);
}
else
{
    XTrace.WriteLine("启动成功！PID={0}", p.Id);
}

Console.WriteLine("TestA OK!");
//Console.ReadKey();
Thread.Sleep(5_000);