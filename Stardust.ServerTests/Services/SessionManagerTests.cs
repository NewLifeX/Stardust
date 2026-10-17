using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using NewLife.Log;
using NewLife.Remoting.Models;
using NewLife.Remoting.Services;
using Stardust.Server.Services;
using Xunit;

namespace ServerTest.Services;

/// <summary>应用与节点会话管理器单元测试。覆盖消息主题约定以及会话的登记、查询与移除</summary>
public class SessionManagerTests
{
    /// <summary>伪造应用生命周期，提供 ApplicationStopping 通知</summary>
    private sealed class FakeLifetime : IHostApplicationLifetime
    {
        private readonly CancellationTokenSource _stopping = new();

        public CancellationToken ApplicationStarted => CancellationToken.None;
        public CancellationToken ApplicationStopping => _stopping.Token;
        public CancellationToken ApplicationStopped => CancellationToken.None;
        public void StopApplication() => _stopping.Cancel();
    }

    /// <summary>只提供 IHostApplicationLifetime 的服务容器</summary>
    private sealed class FakeProvider : IServiceProvider
    {
        private readonly IHostApplicationLifetime _lifetime = new FakeLifetime();

        public Object GetService(Type serviceType) => serviceType == typeof(IHostApplicationLifetime) ? _lifetime : null;
    }

    /// <summary>无真实网络连接的命令会话</summary>
    private sealed class FakeSession : ICommandSession
    {
        public String Code { get; set; } = null!;
        public Boolean Active { get; set; } = true;

        public Task HandleAsync(CommandModel command, String message, CancellationToken cancellationToken) => Task.CompletedTask;

        public void Dispose() { }
    }

    [Fact(DisplayName = "节点会话管理器使用NodeCommands主题")]
    public void NodeManager_UsesNodeTopic()
    {
        using var manager = new NodeSessionManager(new FakeProvider());

        Assert.Equal("NodeCommands", manager.Topic);

        var session = new FakeSession { Code = "node-1" };
        manager.Add(session);

        Assert.Same(session, manager.Get("node-1"));

        manager.Remove(session);
        Assert.Null(manager.Get("node-1"));
    }

    [Fact(DisplayName = "应用会话管理器使用AppCommands主题")]
    public void AppManager_UsesAppTopic()
    {
        using var manager = new AppSessionManager(new FakeProvider(), DefaultTracer.Instance, Logger.Null);

        Assert.Equal("AppCommands", manager.Topic);

        var session = new FakeSession { Code = "app-1" };
        manager.Add(session);

        Assert.Same(session, manager.Get("app-1"));

        manager.Remove(session);
        Assert.Null(manager.Get("app-1"));
    }

    [Fact(DisplayName = "重复登记同一标识时后加入的会话覆盖前者")]
    public void AppManager_AddDuplicateCode_Overwrites()
    {
        using var manager = new AppSessionManager(new FakeProvider(), DefaultTracer.Instance, Logger.Null);

        var first = new FakeSession { Code = "dup" };
        var second = new FakeSession { Code = "dup" };
        manager.Add(first);
        manager.Add(second);

        Assert.Same(second, manager.Get("dup"));
    }
}
