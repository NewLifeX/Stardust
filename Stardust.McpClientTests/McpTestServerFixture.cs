using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NewLife;
using NewLife.Log;
using Stardust.Server;
using Xunit;

namespace Stardust.McpClientTests;

/// <summary>测试服务器Fixture。启动Stardust.Web进程内实例，启用MCP服务</summary>
public class McpTestServerFixture : IAsyncLifetime
{
    private TestServer? _server;
    private HttpClient? _cookieLessClient;

    /// <summary>测试服务器基础地址</summary>
    public String BaseUrl => "http://localhost/";

    /// <summary>HTTP客户端（连接到TestServer）。统一去除Cookie，避免TestServer共享CookieContainer
    /// 累积的畸形设备ID等Cookie触发 UseStardust/Cube 等前置中间件异常，干扰 /mcp 机器API测试。</summary>
    public HttpClient CreateClient()
    {
        if (_server == null) throw new InvalidOperationException("TestServer not initialized");
        if (_cookieLessClient == null)
        {
            var handler = new NoCookieHandler { InnerHandler = _server.CreateHandler() };
            _cookieLessClient = new HttpClient(handler) { BaseAddress = new Uri(BaseUrl) };
        }
        return _cookieLessClient;
    }

    /// <summary>剥离请求中的Cookie并忽略响应Set-Cookie，使每个请求互不携带会话态</summary>
    private sealed class NoCookieHandler : DelegatingHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            request.Headers.Remove("Cookie");
            var response = await base.SendAsync(request, cancellationToken);
            response.Headers.Remove("Set-Cookie");
            return response;
        }
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

        // 安装测试专用日志捕获器：接管 XTrace 输出，缓冲服务端日志供测试核对（转发原日志保留控制台输出）
        McpTestLog.Install(XTrace.Log);

        await Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _server?.Dispose();
        return Task.CompletedTask;
    }
}
