Process启动进程研究

## 研究目标

A应用通过Process类启动B应用，研究不同参数设置下的测试结果。



## 测试准备

.Net版本：net8.0

星尘代理：/root/agent

A目录：/root/TestA  主程序

B目录：/root/TestB  目标NewLife应用

C目录：/root/TestC  目标net8应用

D目录：/root/TestD  目标非托管应用

跟随退出：随着A应用退出，B应用跟随退出

测试逻辑：A应用设置不同参数，启动B应用，然后A先退出，观察B是否跟随退出。



测试程序：

```c#
using System.Diagnostics;
using NewLife;
using NewLife.Log;

XTrace.UseConsole();

XTrace.WriteLine("TestA启动，PID={0}", Process.GetCurrentProcess().Id);
XTrace.WriteLine("测试参数：{0}", args.Join(" "));

var target = "TestB";
if (args.Contains("-c"))
    target = "TestC";

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
```



## 分类测试

根据不同类型的B应用，分类测试。

#### NewLife应用测试

要求B应用必须引入NewLife.Core，它能收到环境变量BasePath并自动调整当前目录。

win10执行命令的目录：D:\X\Stardust\Bin\Samples

centos执行命令的目录：/root

| 系统/参数          | Shell | WorkingDirectory | Environment     | 合并输出 | 跟随退出 | 结果CurrentDirectory |
| ------------------ | :---: | ---------------- | --------------- | :------: | :------: | -------------------- |
| win10              | false |                  |                 |    Y     |    N     | \Samples             |
| TestA.exe -b -e    | false |                  | star=dust       |    Y     |    N     | \Samples             |
| TestA.exe -w       | false | \Samples\TestB   |                 |    Y     |    N     | \Samples\TestB       |
| TestA.exe -w -b -e | false | \Samples\TestB   | BasePath=xxx    |    Y     |    N     | \Samples\TestB       |
| TestA.exe -s       | true  |                  |                 |    N     |    N     | \Samples             |
| TestA.exe -s -b -e | true  |                  | 要求shell=false |          |          | ==报错==             |
| TestA.exe -s -w    | true  | \Samples\TestB   |                 |    N     |    N     | \Samples\TestB       |
| TestA.exe -s -w -b | true  | \Samples\TestB   | BasePath=xxx    |    N     |    N     | \Samples\TestB       |
| CentOS7.9          | false |                  |                 |    Y     |    N     | /root                |
| TestA -b -e        | false |                  | star=dust       |    Y     |    N     | /root                |
| TestA -w           | false | /root/TestB      |                 |    Y     |    N     | /root/TestB          |
| TestA -w -b -e     | false | /root/TestB      | BasePath=xxx    |    Y     |    N     | /root/TestB          |
| TestA -s           | true  |                  |                 |    Y     |    N     | /root                |
| TestA -s -b -e     | true  |                  | BasePath=xxx    |    Y     |    N     | /root                |
| TestA -s -w        | true  | /root/TestB      |                 |    Y     |    N     | /root/TestB          |
| TestA -s -w -b -e  | true  | /root/TestB      | BasePath=xxx    |    Y     |    N     | /root/TestB          |

测试结论：

1. 目标B应用的当前目录，取决于WorkingDirectory，如果未设置则取A应用的当前目录（非A工作目录）

2. windows上UseShellExecute=true时，目标B应用输出不会合并到A窗口，而是独立窗口

3. 所有测试用例，B都不会跟随A退出。（该结论跟星尘代理现状不一致，后者会跟随退出）

4. 进程的Environment环境变量，在windows下要求UseShellExecute=false，Linux下则无此要求

   

#### Net8应用测试

要求C应用是普通net8应用，禁止引入NewLife.Core。

win10执行命令的目录：D:\X\Stardust\Bin\Samples

centos执行命令的目录：/root

| 系统/参数             | Shell | WorkingDirectory | Environment     | 合并输出 | 跟随退出 | 结果CurrentDirectory |
| --------------------- | :---: | ---------------- | --------------- | :------: | :------: | -------------------- |
| win10                 | false |                  |                 |    Y     |    N     | \Samples             |
| TestA.exe -c -b -e    | false |                  | star=dust       |    Y     |    N     | \Samples             |
| TestA.exe -c -w       | false | \Samples\TestC   |                 |    Y     |    N     | \Samples\TestC       |
| TestA.exe -c -w -b -e | false | \Samples\TestC   | BasePath=xxx    |    Y     |    N     | \Samples\TestC       |
| TestA.exe -c -s       | true  |                  |                 |    N     |    N     | \Samples             |
| TestA.exe -c -s -b -e | true  |                  | 要求shell=false |          |          | ==报错==             |
| TestA.exe -c -s -w    | true  | \Samples\TestC   |                 |    N     |    N     | \Samples\TestC       |
| TestA.exe -c -s -w -b | true  | \Samples\TestC   | BasePath=xxx    |    N     |    N     | \Samples\TestC       |
| CentOS7.9             | false |                  |                 |    Y     |    N     | /root                |
| TestA -c -b -e        | false |                  | star=dust       |    Y     |    N     | /root                |
| TestA -c -w           | false | /root/TestC      |                 |    Y     |    N     | /root/TestC          |
| TestA -c -w -b -e     | false | /root/TestC      | BasePath=xxx    |    Y     |    N     | /root/TestC          |
| TestA -c -s           | true  |                  |                 |    Y     |    N     | /root                |
| TestA -c -s -b -e     | true  |                  | BasePath=xxx    |    Y     |    N     | /root                |
| TestA -c -s -w        | true  | /root/TestC      |                 |    Y     |    N     | /root/TestC          |
| TestA -c -s -w -b -e  | true  | /root/TestC      | BasePath=xxx    |    Y     |    N     | /root/TestC          |

测试结论：

1. net8应用测试表现跟NewLife应用一致

2. 目标C应用的当前目录，取决于WorkingDirectory，如果未设置则取A应用的当前目录（非A工作目录）

3. windows上UseShellExecute=true时，目标C应用输出不会合并到A窗口，而是独立窗口

4. 所有测试用例，C都不会跟随A退出。（该结论跟星尘代理现状不一致，后者会跟随退出）

5. 进程的Environment环境变量，在windows下要求UseShellExecute=false，Linux下则无此要求

   

#### 非托管应用测试

要求B应用必须是非托管应用。

win10执行命令的目录：D:\X\Stardust\Bin\Samples

centos执行命令的目录：/root

| 系统/参数             | Shell | WorkingDirectory | Environment     | 合并输出 | 跟随退出 | 结果CurrentDirectory |
| --------------------- | :---: | ---------------- | --------------- | :------: | :------: | -------------------- |
| win10                 | false |                  |                 |    Y     |    N     | \Samples             |
| TestA.exe -d -b -e    | false |                  | star=dust       |    Y     |    N     | \Samples             |
| TestA.exe -d -w       | false | \Samples\TestD   |                 |    Y     |    N     | \Samples\TestD       |
| TestA.exe -d -w -b -e | false | \Samples\TestD   | BasePath=xxx    |    Y     |    N     | \Samples\TestD       |
| TestA.exe -d -s       | true  |                  |                 |    N     |    N     | \Samples             |
| TestA.exe -d -s -b -e | true  |                  | 要求shell=false |          |          | ==报错==             |
| TestA.exe -d -s -w    | true  | \Samples\TestD   |                 |    N     |    N     | \Samples\TestD       |
| TestA.exe -d -s -w -b | true  | \Samples\TestD   | BasePath=xxx    |    N     |    N     | \Samples\TestD       |
| CentOS7.9             | false |                  |                 |    Y     |    N     | /root                |
| TestA -d -b -e        | false |                  | star=dust       |    Y     |    N     | /root                |
| TestA -d -w           | false | /root/TestD      |                 |    Y     |    N     | /root/TestD          |
| TestA -d -w -b -e     | false | /root/TestD      | BasePath=xxx    |    Y     |    N     | /root/TestD          |
| TestA -d -s           | true  |                  |                 |    Y     |    N     | /root                |
| TestA -d -s -b -e     | true  |                  | BasePath=xxx    |    Y     |    N     | /root                |
| TestA -d -s -w        | true  | /root/TestD      |                 |    Y     |    N     | /root/TestD          |
| TestA -d -s -w -b -e  | true  | /root/TestD      | BasePath=xxx    |    Y     |    N     | /root/TestD          |

测试结论：

1. 目标B应用的当前目录，取决于WorkingDirectory，如果未设置则取A应用的当前目录（非A工作目录）

2. windows上UseShellExecute=true时，目标B应用输出不会合并到A窗口，而是独立窗口

3. 所有测试用例，B都不会跟随A退出。（该结论跟星尘代理现状不一致，后者会跟随退出）

4. 进程的Environment环境变量，在windows下要求UseShellExecute=false，Linux下则无此要求

   