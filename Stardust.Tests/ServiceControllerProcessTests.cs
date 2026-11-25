using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using NewLife;
using NewLife.Log;
using NewLife.Security;
using Stardust.Managers;
using Stardust.Models;
using Xunit;

namespace Stardust.Tests;

/// <summary>应用进程管理测试。覆盖真实拉起/停止进程与进程信息映射（NODE-17）</summary>
public class ServiceControllerProcessTests
{
    /// <summary>构造一个能存活数秒的跨平台命令</summary>
    private static (String file, String args) LongRunningCommand() =>
        Runtime.Windows ? ("cmd", "/c ping -n 20 127.0.0.1 > nul") : ("sh", "-c \"sleep 20\"");

    private static String CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "stardust-proc-" + Guid.NewGuid().ToString("n")[..8]);
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static ServiceController CreateController(String workDir)
    {
        var (file, args) = LongRunningCommand();
        var name = "proc_" + Rand.NextString(6);
        var controller = new ServiceController { Name = name, StartWait = 500 };
        controller.SetInfo(new ServiceInfo
        {
            Name = name,
            FileName = file,
            Arguments = args,
            WorkingDirectory = workDir,
            Enable = true,
            Mode = DeployMode.Standard,
        });

        return controller;
    }

    [Fact(DisplayName = "启动真实进程后状态为运行中，停止后进程退出")]
    public void StartStop_RealProcess()
    {
        var dir = CreateTempDir();
        var controller = CreateController(dir);
        try
        {
            Assert.True(controller.Start());
            Assert.True(controller.Running);
            Assert.NotNull(controller.Process);
            Assert.False(controller.Process!.HasExited);

            // 进程信息映射包含真实的进程编号
            var model = controller.ToModel();
            Assert.NotNull(model);
            Assert.True(model.ProcessId > 0, $"进程编号应大于0，实际 {model.ProcessId}");
            Assert.False(model.ProcessName.IsNullOrEmpty());

            controller.Stop("test");
            Assert.False(controller.Running);
            Assert.Null(controller.Process);
        }
        finally
        {
            controller.TryDispose();
            try { Directory.Delete(dir, true); } catch { }
        }
    }

    [Fact(DisplayName = "缺少文件名时启动失败")]
    public void Start_EmptyFileName_Fail()
    {
        var dir = CreateTempDir();
        var controller = new ServiceController { Name = "proc_" + Rand.NextString(6) };
        controller.SetInfo(new ServiceInfo { Name = controller.Name, FileName = "", WorkingDirectory = dir, Enable = true });
        try
        {
            Assert.False(controller.Start());
            Assert.False(controller.Running);
        }
        finally
        {
            controller.TryDispose();
            try { Directory.Delete(dir, true); } catch { }
        }
    }

    [Fact(DisplayName = "未设置服务信息时启动失败")]
    public void Start_NoInfo_Fail()
    {
        var controller = new ServiceController { Name = "proc_" + Rand.NextString(6) };

        Assert.False(controller.Start());
        Assert.False(controller.Running);
    }

    [Fact(DisplayName = "停止外部接管的进程不会误报失败")]
    public void Stop_ExternalProcess_NoFalseFailure()
    {
        var (file, args) = LongRunningCommand();
        var started = Process.Start(new ProcessStartInfo(file, args) { UseShellExecute = false });
        Assert.NotNull(started);

        var pid = started!.Id;
        started.Dispose();

        // 模拟从进程编号接管外部启动的进程，此时读取 ExitCode 会抛“Process was not started by this object”
        var external = Process.GetProcessById(pid);
        var messages = new List<String>();
        var controller = new ServiceController
        {
            Name = "proc_" + Rand.NextString(6),
            Log = new ActionLog((format, values) =>
            {
                lock (messages)
                {
                    try { messages.Add(values == null || values.Length == 0 ? format : String.Format(format, values)); }
                    catch { messages.Add(format); }
                }
            }),
        };
        try
        {
            controller.Process = external;
            controller.Stop("test");

            Assert.True(external.HasExited);
            Assert.DoesNotContain(messages, e => e.Contains("Failed to exit process"));
        }
        finally
        {
            try { if (!external.HasExited) external.Kill(); } catch { }
            controller.TryDispose();
            external.Dispose();
        }
    }

    private static MethodInfo GetPrivateMethod(String name)
    {
        var method = typeof(ServiceController).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        return method!;
    }

    private static String? InvokeGetHostEntryFile(String[] args, Boolean skipFirst = false)
    {
        var method = GetPrivateMethod("GetHostEntryFile");
        return method.Invoke(null, [args, skipFirst]) as String;
    }

    private static Boolean InvokeIsHostProcess(String processName)
    {
        var method = GetPrivateMethod("IsHostProcess");
        return (Boolean)method.Invoke(null, [processName])!;
    }

    [Fact(DisplayName = "node/python 等共享运行时被识别为宿主进程，普通进程不受影响")]
    public void IsHostProcess_SharedRuntimes()
    {
        Assert.True(InvokeIsHostProcess("node"));
        Assert.True(InvokeIsHostProcess("node.exe"));
        Assert.True(InvokeIsHostProcess("python"));
        Assert.True(InvokeIsHostProcess("python3"));
        Assert.True(InvokeIsHostProcess("java"));
        Assert.True(InvokeIsHostProcess("dotnet"));

        Assert.False(InvokeIsHostProcess("nginx"));
        Assert.False(InvokeIsHostProcess("myservice"));
    }

    [Fact(DisplayName = "node 进程命令行跳过开关后提取入口脚本")]
    public void GetHostEntryFile_Node()
    {
        // 进程命令行首项是宿主可执行文件
        Assert.Equal("server.js", InvokeGetHostEntryFile([@"C:\nvm4w\nodejs\node.exe", "server.js", "--port", "8088"], true));

        // 带取值开关时仍能定位脚本，避免把开关取值当成入口
        Assert.Equal("server.js", InvokeGetHostEntryFile([@"C:\nvm4w\nodejs\node.exe", "--max-old-space-size", "4096", "server.js"], true));

        // 配置参数没有宿主可执行文件首项
        Assert.Equal("server.js", InvokeGetHostEntryFile(["server.js", "--port", "8088"]));
    }

    [Fact(DisplayName = "python/java/dotnet 命令行提取入口文件")]
    public void GetHostEntryFile_Hosts()
    {
        // -m 模块方式没有入口文件
        Assert.Null(InvokeGetHostEntryFile(["python.exe", "-m", "http.server"], true));
        Assert.Equal("app.py", InvokeGetHostEntryFile(["python.exe", "-u", "app.py"], true));

        Assert.Equal("app.jar", InvokeGetHostEntryFile(["java", "-jar", "app.jar"], true));

        Assert.Equal("app.dll", InvokeGetHostEntryFile(["dotnet", "exec", "app.dll"], true));
        Assert.Equal("app.dll", InvokeGetHostEntryFile(["app.dll", "--urls", "http://localhost:5000"]));
    }

    [Fact(DisplayName = "单实例模式不接管可执行文件路径不一致的同名进程")]
    public void Check_SameName_DifferentPath_NotTakeOver()
    {
        // 两份同名可执行文件放在不同目录，用唯一名称避免与系统其它进程混淆
        var dirA = CreateTempDir();
        var dirB = CreateTempDir();
        var exeName = "sdp" + Rand.NextString(5) + ".exe";
        var exeA = Path.Combine(dirA, exeName);
        var exeB = Path.Combine(dirB, exeName);
        var src = Runtime.Windows ? Path.Combine(Environment.SystemDirectory, "cmd.exe") : "/bin/sh";
        File.Copy(src, exeA);
        File.Copy(src, exeB);
        if (!OperatingSystem.IsWindows())
        {
            const UnixFileMode mode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
            File.SetUnixFileMode(exeA, mode);
            File.SetUnixFileMode(exeB, mode);
        }

        var (_, args) = LongRunningCommand();
        var started = Process.Start(new ProcessStartInfo(exeA, args) { UseShellExecute = false });
        Assert.NotNull(started);
        var pidA = started!.Id;
        var processName = started.ProcessName;
        started.Dispose();

        var controller = new ServiceController { Name = "proc_" + Rand.NextString(6), StartWait = 500, ProcessName = processName };
        controller.SetInfo(new ServiceInfo
        {
            Name = controller.Name,
            FileName = exeB,
            Arguments = args,
            WorkingDirectory = dirB,
            Enable = true,
            Mode = DeployMode.Standard,
        });

        try
        {
            // 旧逻辑在单实例模式下会直接接管第一个同名进程，即使可执行文件路径完全不同
            Assert.True(controller.Check());
            Assert.NotNull(controller.Process);
            Assert.NotEqual(pidA, controller.Process!.Id);
            Assert.Equal(exeB, controller.Process.MainModule?.FileName, true);
        }
        finally
        {
            controller.Stop("test");
            controller.TryDispose();
            try { Process.GetProcessById(pidA).Kill(); } catch { }
            try { Directory.Delete(dirA, true); } catch { }
            try { Directory.Delete(dirB, true); } catch { }
        }
    }
}
