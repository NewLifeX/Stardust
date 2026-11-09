using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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
}
