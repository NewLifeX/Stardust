using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using NewLife;
using Stardust.Monitors;
using Xunit;

namespace Stardust.Tests;

/// <summary>浏览器埋点中转客户端测试。用本地服务端桩验证登录、转发、客户端IP与体积上限</summary>
public class BrowserRelayClientTests
{
    /// <summary>极简星尘服务端桩：OAuth/Token 发放令牌，Trace/ReportRelay 记录请求</summary>
    private sealed class RelayServerStub : IDisposable
    {
        private readonly HttpListener _listener = new();

        public String Server { get; }
        public Int32 TokenCalls { get; private set; }
        public Int32 RelayCalls { get; private set; }
        public String LastQuery { get; private set; }
        public String LastAuthorization { get; private set; }
        public String LastBody { get; private set; }

        public RelayServerStub()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();

            Server = $"http://127.0.0.1:{port}";
            _listener.Prefixes.Add(Server + "/");
            _listener.Start();

            _ = Task.Run(LoopAsync);
        }

        private async Task LoopAsync()
        {
            while (_listener.IsListening)
            {
                HttpListenerContext ctx;
                try
                {
                    ctx = await _listener.GetContextAsync();
                }
                catch
                {
                    return;
                }

                await RespondAsync(ctx);
            }
        }

        private async Task RespondAsync(HttpListenerContext ctx)
        {
            try
            {
                var path = ctx.Request.Url?.AbsolutePath;
                String json;
                if (path.EqualIgnoreCase("/OAuth/Token"))
                {
                    TokenCalls++;
                    json = "{\"code\":0,\"data\":{\"access_token\":\"stub-token\",\"token_type\":\"Bearer\",\"expire_in\":3600}}";
                }
                else if (path.EqualIgnoreCase("/Trace/ReportRelay"))
                {
                    RelayCalls++;
                    LastQuery = ctx.Request.Url?.Query;
                    LastAuthorization = ctx.Request.Headers["Authorization"];
                    using var reader = new StreamReader(ctx.Request.InputStream, Encoding.UTF8);
                    LastBody = await reader.ReadToEndAsync();
                    json = "{\"code\":0,\"data\":{\"period\":60}}";
                }
                else
                {
                    json = "{\"code\":404}";
                }

                var buf = Encoding.UTF8.GetBytes(json);
                ctx.Response.ContentType = "application/json";
                ctx.Response.ContentLength64 = buf.Length;
                await ctx.Response.OutputStream.WriteAsync(buf, 0, buf.Length);
                ctx.Response.Close();
            }
            catch
            {
                // 客户端提前断开时忽略
            }
        }

        public void Dispose()
        {
            try { _listener.Stop(); } catch { }
        }
    }

    [Fact(DisplayName = "中转客户端自动登录并把报文与客户端IP转发到星尘")]
    public async Task ForwardAsync_LoginsAndForwards()
    {
        using var stub = new RelayServerStub();
        using var client = new BrowserRelayClient(stub.Server, "RelayApp", "relay-secret");

        const String payload = "{\"AppId\":\"RelayApp\",\"Builders\":[]}";
        using var body = new MemoryStream(Encoding.UTF8.GetBytes(payload));

        var rs = await client.ForwardAsync(body, "203.0.113.7");

        Assert.NotNull(rs);
        Assert.Equal(60, rs.Period);
        Assert.Equal(1, stub.TokenCalls);
        Assert.Equal(1, stub.RelayCalls);
        Assert.Contains("clientIp=203.0.113.7", stub.LastQuery);
        Assert.Equal("Bearer stub-token", stub.LastAuthorization);
        Assert.Equal(payload, stub.LastBody);

        // 第二次复用已登录令牌
        using var body2 = new MemoryStream(Encoding.UTF8.GetBytes(payload));
        await client.ForwardAsync(body2, null);

        Assert.Equal(1, stub.TokenCalls);
        Assert.Equal(2, stub.RelayCalls);
    }

    [Fact(DisplayName = "报文超过上限时不发起转发")]
    public async Task ForwardAsync_TooLarge_Throws()
    {
        using var stub = new RelayServerStub();
        using var client = new BrowserRelayClient(stub.Server, "RelayApp", "relay-secret") { MaxBodySize = 16 };

        using var body = new MemoryStream(Encoding.UTF8.GetBytes(new String('x', 64)));

        await Assert.ThrowsAsync<InvalidOperationException>(() => client.ForwardAsync(body, null));
        Assert.Equal(0, stub.RelayCalls);
        Assert.Equal(0, stub.TokenCalls);
    }

    [Fact(DisplayName = "未配置服务端地址或应用标识时明确报错")]
    public async Task ForwardAsync_MissingConfig_Throws()
    {
        using var client = new BrowserRelayClient("", "", "");
        using var body = new MemoryStream([1, 2, 3]);

        await Assert.ThrowsAsync<InvalidOperationException>(() => client.ForwardAsync(body, null));
    }
}
