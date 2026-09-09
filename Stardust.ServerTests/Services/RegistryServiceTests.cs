using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
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
}
