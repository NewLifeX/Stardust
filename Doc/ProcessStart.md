Process启动进程研究

## 研究目标

A应用通过Process类启动B应用，研究不同参数设置下的测试结果。



## 测试准备

.Net版本：net8.0

星尘代理：/root/agent

A目录：/root/testA

B目录：/root/testB

跟随退出：随着A应用退出，B应用跟随退出



测试程序：

```c#
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
    FileName = Runtime.Windows ? $"../{target}/{target}.exe" : $"../{target}/{target}",
    Arguments = "-name NewLife",
    //WorkingDirectory = "",
    //UseShellExecute = false,
};

if (args.Contains("-s")) si.UseShellExecute = true;
if (args.Contains("-w")) si.WorkingDirectory = ".";
if (args.Contains("-e")) si.Environment["star"] = "dust";

Environment.SetEnvironmentVariable("BasePath", si.WorkingDirectory.GetFullPath());

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
```



## 分类测试

根据不同类型的B应用，分类测试。

#### NewLife应用测试

要求B应用必须引入NewLife.Core，它能收到环境变量BasePath并自动调整当前目录。

| 系统/参数          | Shell | WorkingDirectory | Environment     | 合并输出 | 跟随退出 | 结果CurrentDirectory |
| ------------------ | :---: | ---------------- | --------------- | :------: | :------: | -------------------- |
| win10              | false |                  |                 |    Y     |    N     | \Samples             |
| TestA.exe -b -e    | false |                  | star=dust       |    Y     |    N     | \Samples             |
| TestA.exe -w       | false | \Samples\TestB   |                 |    Y     |    N     | \Samples\TestB       |
| TestA.exe -w -b -e | false | \Samples\TestB   | BasePath=xxx    |    Y     |    N     | \Samples\TestB       |
| TestA.exe -s       | true  |                  |                 |    N     |    N     | \Samples             |
| TestA.exe -s -b -e | true  |                  | 要求shell=false |          |          | 报错                 |
| TestA.exe -s -w    | true  | \Samples\TestB   |                 |    N     |    N     | \Samples\TestB       |
| TestA.exe -s -w -b | true  | \Samples\TestB   | BasePath=xxx    |    N     |    N     | \Samples\TestB       |



#### Net8应用测试

要求B应用是普通net8应用，禁止引入NewLife.Core。



#### 非托管应用测试

要求B应用必须是非托管应用。

