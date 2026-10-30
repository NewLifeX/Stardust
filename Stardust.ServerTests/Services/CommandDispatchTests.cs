using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using NewLife.Log;
using NewLife.Remoting.Models;
using NewLife.Remoting.Services;
using NewLife.Security;
using Stardust.Data;
using Stardust.Data.Nodes;
using Stardust.Server.Services;
using Xunit;

namespace ServerTest.Services;

/// <summary>命令下发测试。用伪会话验证应用/节点命令被写入库并通过会话通道下发（APP-5 / NODE-9）</summary>
public class CommandDispatchTests
{
    #region 伪会话基础设施
    private sealed class FakeLifetime : IHostApplicationLifetime
    {
        public CancellationToken ApplicationStarted => CancellationToken.None;
        public CancellationToken ApplicationStopping => CancellationToken.None;
        public CancellationToken ApplicationStopped => CancellationToken.None;
        public void StopApplication() { }
    }

    private sealed class FakeProvider : IServiceProvider
    {
        private readonly IHostApplicationLifetime _lifetime = new FakeLifetime();

        public Object GetService(Type serviceType) => serviceType == typeof(IHostApplicationLifetime) ? _lifetime : null;
    }

    /// <summary>记录收到命令的伪会话</summary>
    private sealed class RecordingSession : ICommandSession
    {
        public String Code { get; set; } = null!;
        public Boolean Active { get; set; } = true;

        public List<CommandModel> Commands { get; } = [];

        public Task HandleAsync(CommandModel command, String message, CancellationToken cancellationToken)
        {
            if (command != null) Commands.Add(command);

            return Task.CompletedTask;
        }

        public void Dispose() { }
    }
    #endregion

    /// <summary>沿继承链设置私有字段</summary>
    private static void SetField(Object target, String name, Object value)
    {
        for (var type = target.GetType(); type != null; type = type.BaseType)
        {
            var fi = type.GetField(name, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
            if (fi != null)
            {
                fi.SetValue(target, value);
                return;
            }
        }

        throw new InvalidOperationException("字段不存在：" + name);
    }

    /// <summary>等待伪会话收到命令，最多十秒。全量并行测试时机器繁忙，两秒容易超时</summary>
    private static async Task<CommandModel> WaitCommandAsync(RecordingSession session)
    {
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < 10_000)
        {
            lock (session.Commands)
            {
                if (session.Commands.Count > 0) return session.Commands[0];
            }

            await Task.Delay(20);
        }

        return null;
    }

    [Fact(DisplayName = "应用命令写入AppCommand并经会话通道下发")]
    public async Task AppCommand_DispatchedToSession()
    {
        var manager = new AppSessionManager(new FakeProvider(), DefaultTracer.Instance, Logger.Null);
        var svc = (RegistryService)RuntimeHelpers.GetUninitializedObject(typeof(RegistryService));
        SetField(svc, "_sessionManager", manager);

        var app = new App { Name = "cmd_" + Rand.NextString(8), Enable = true };
        app.Insert();
        var client = "c_" + Rand.NextString(6);
        var online = new AppOnline { AppId = app.Id, Name = app.Name, Client = client };
        online.Insert();

        var session = new RecordingSession { Code = $"{app.Name}@{client}" };
        manager.Add(session);

        AppCommand cmd = null;
        try
        {
            var reply = await svc.SendCommand(app, null, new CommandInModel { Command = "TestCommand", Argument = "-v", Timeout = 0 }, "tester");

            // 即发即忘时返回空，命令已下发
            Assert.Null(reply);

            var received = await WaitCommandAsync(session);
            Assert.NotNull(received);
            Assert.Equal("TestCommand", received.Command);

            // 命令记录已落库
            cmd = AppCommand.Find(AppCommand._.AppId == app.Id);
            Assert.NotNull(cmd);
            Assert.Equal("TestCommand", cmd.Command);
        }
        finally
        {
            manager.Remove(session);
            cmd?.Delete();
            online.Delete();
            app.Delete();
        }
    }

    [Fact(DisplayName = "节点命令写入NodeCommand并经会话通道下发，返回命令编号")]
    public async Task NodeCommand_DispatchedToSession()
    {
        var manager = new NodeSessionManager(new FakeProvider());
        var svc = (NodeService)RuntimeHelpers.GetUninitializedObject(typeof(NodeService));
        SetField(svc, "_sessionManager", manager);

        var node = new Node { Code = "nd_" + Rand.NextString(8), Name = "node-" + Rand.NextString(4), Enable = true };
        node.Insert();

        var session = new RecordingSession { Code = node.Code };
        manager.Add(session);

        NodeCommand cmd = null;
        try
        {
            var reply = await svc.SendCommand(node, new CommandInModel { Command = "Restart", Argument = "-f", Timeout = 0 }, "tester");

            // 即发即忘仍返回命令编号，供流水线按回包事件命中
            Assert.NotNull(reply);
            Assert.True(reply.Id > 0);

            var received = await WaitCommandAsync(session);
            Assert.NotNull(received);
            Assert.Equal("Restart", received.Command);

            cmd = NodeCommand.FindById((Int32)reply.Id);
            Assert.NotNull(cmd);
            Assert.Equal("Restart", cmd.Command);
        }
        finally
        {
            manager.Remove(session);
            cmd?.Delete();
            node.Delete();
        }
    }

    [Fact(DisplayName = "应用无在线实例时下发直接返回空")]
    public async Task AppCommand_NoOnline_ReturnsNull()
    {
        var manager = new AppSessionManager(new FakeProvider(), DefaultTracer.Instance, Logger.Null);
        var svc = (RegistryService)RuntimeHelpers.GetUninitializedObject(typeof(RegistryService));
        SetField(svc, "_sessionManager", manager);

        var app = new App { Name = "cmd_" + Rand.NextString(8), Enable = true };
        app.Insert();

        AppCommand cmd = null;
        try
        {
            var reply = await svc.SendCommand(app, null, new CommandInModel { Command = "NoOnline", Timeout = 0 }, "tester");

            Assert.Null(reply);

            // 命令记录仍然落库，便于审计
            cmd = AppCommand.Find(AppCommand._.AppId == app.Id);
            Assert.NotNull(cmd);
        }
        finally
        {
            cmd?.Delete();
            app.Delete();
        }
    }
}
