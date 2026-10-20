using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using NewLife.Log;
using NewLife.Remoting.Models;
using Stardust.Data;
using Stardust.Server.Services;
using Xunit;

namespace ServerTest.Services;

/// <summary>注册服务单元测试</summary>
public class RegistryServiceTests
{
    /// <summary>向没有任何在线实例的应用下发命令时，应返回空而非抛空引用异常。
    /// 回归：SendCommand 在 ts 为空时执行 await null，抛 NullReferenceException，
    /// 导致 App/RegisterService 在通知消费者（NotifyConsumers）时报500。</summary>
    [Fact]
    [Trait("Category", "Registry")]
    public async Task SendCommand_NoOnlineInstance_ReturnNull()
    {
        var name = "test-sendcmd-" + Guid.NewGuid().ToString("n")[..8];
        var app = new App
        {
            Name = name,
            Secret = "test-secret",
            Enable = true,
        };
        app.Insert();

        try
        {
            // 跳过构造函数（依赖过多），仅验证无在线实例的分支
            var svc = (RegistryService)RuntimeHelpers.GetUninitializedObject(typeof(RegistryService));

            var model = new CommandInModel
            {
                Command = "test/echo",
                Argument = "hello",
            };

            var rs = await svc.SendCommand(app, null, model, "tester");

            // 没有在线实例时不能抛异常，应返回空
            Assert.Null(rs);
        }
        finally
        {
            // 清理 SendCommand 内部插入的命令与应用
            AppCommand.Delete(AppCommand._.AppId == app.Id);
            app.Delete();
        }
    }

    /// <summary>应用存在在线实例、但命令即发即忘（timeout<=0）无人回包时，应返回空而非抛空引用异常。
    /// 回归：SendCommand 在任务列表非空但全部结果为 null 时，await FirstOrDefault(...) 拿到 null 任务，
    /// 抛 NullReferenceException，导致 App/RegisterService 在通知消费者（NotifyConsumers）时报500。</summary>
    [Fact]
    [Trait("Category", "Registry")]
    public async Task SendCommand_OnlineInstanceWithoutReply_ReturnNull()
    {
        var name = "test-sendcmd-" + Guid.NewGuid().ToString("n")[..8];
        var app = new App
        {
            Name = name,
            Secret = "test-secret",
            Enable = true,
        };
        app.Insert();

        var online = new AppOnline
        {
            AppId = app.Id,
            Name = name,
            Client = "10.0.0.1@1234",
            CreateTime = DateTime.Now,
            UpdateTime = DateTime.Now,
        };
        online.Insert();

        try
        {
            // 跳过构造函数（依赖过多），注入无会话的会话管理器，使 PublishAsync 立即返回 null
            var svc = (RegistryService)RuntimeHelpers.GetUninitializedObject(typeof(RegistryService));
            using var sessions = new AppSessionManager(new FakeProvider(), DefaultTracer.Instance, Logger.Null);
            typeof(RegistryService).GetField("_sessionManager", BindingFlags.NonPublic | BindingFlags.Instance)!
                .SetValue(svc, sessions);

            var model = new CommandInModel
            {
                Command = "test/echo",
                Argument = "hello",
            };

            var rs = await svc.SendCommand(app, null, model, "tester");

            // 在线实例没有回包时不能抛异常，应返回空
            Assert.Null(rs);
        }
        finally
        {
            // 清理 SendCommand 内部插入的命令、在线记录与应用
            AppCommand.Delete(AppCommand._.AppId == app.Id);
            AppOnline.Delete(AppOnline._.AppId == app.Id);
            app.Delete();
        }
    }

    /// <summary>最小服务提供器：仅满足 AppSessionManager 构造时对 IHostApplicationLifetime 的依赖，其余一律返回 null</summary>
    private sealed class FakeProvider : IServiceProvider
    {
        private readonly IHostApplicationLifetime _lifetime = new FakeLifetime();

        public Object GetService(Type serviceType) => serviceType == typeof(IHostApplicationLifetime) ? _lifetime : null;
    }

    /// <summary>最小化的宿主生命周期，避免构造会话管理器时空引用</summary>
    private sealed class FakeLifetime : IHostApplicationLifetime
    {
        public CancellationToken ApplicationStarted => CancellationToken.None;
        public CancellationToken ApplicationStopping => CancellationToken.None;
        public CancellationToken ApplicationStopped => CancellationToken.None;
        public void StopApplication() { }
    }
}
