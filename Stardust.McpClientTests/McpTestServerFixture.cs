using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Stardust.Server;
using Xunit;

namespace Stardust.McpClientTests;

/// <summary>测试服务器Fixture。启动Stardust.Web进程内实例，启用MCP服务</summary>
public class McpTestServerFixture : IAsyncLifetime
{
    private TestServer? _server;

    /// <summary>测试服务器基础地址</summary>
    public String BaseUrl => "http://localhost/";

    /// <summary>HTTP客户端（连接到TestServer）</summary>
    public HttpClient CreateClient()
    {
        if (_server == null) throw new InvalidOperationException("TestServer not initialized");
        return _server.CreateClient();
    }

    /// <summary>获取TestServer（用于直接访问DI容器）</summary>
    public TestServer Server
    {
        get => _server ?? throw new InvalidOperationException("TestServer not initialized");
        set => _server = value;
    }

    public async Task InitializeAsync()
    {
        // 在服务器启动前启用MCP（仅修改内存值，不调用Save以避免DB未初始化）
        var set = StarServerSetting.Current;
        set.EnableMcp = true;
        set.McpActionSet = "*";

#pragma warning disable CS0618
        var builder = WebHost.CreateDefaultBuilder()
            .UseStartup<Stardust.Web.Startup>()
            .ConfigureTestServices(services =>
            {
                // 确保MCP已启用（Startup会读取StarServerSetting.Current单例）
                var setting = StarServerSetting.Current;
                setting.EnableMcp = true;
                setting.McpActionSet = "*";
            });
#pragma warning restore CS0618

        _server = new TestServer(builder);
        await Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _server?.Dispose();
        return Task.CompletedTask;
    }
}
