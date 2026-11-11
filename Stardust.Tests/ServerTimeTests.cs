using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using NewLife;
using NewLife.Security;
using NewLife.Threading;
using Stardust;
using Xunit;

namespace Stardust.Tests;

/// <summary>服务端探测测试。登录前取服务端时间与默认语言，时间出错时不能让正常客户端久等</summary>
public class ServerTimeTests
{
    [Theory(DisplayName = "解析 /api 返回的服务端信息")]
    [InlineData("{\"serverTime\":1762790400000}", 1762790400000L, null)]
    [InlineData("{\"ServerTime\":1762790400000,\"DefaultLanguage\":\"zh-Hans\"}", 1762790400000L, "zh-Hans")]
    [InlineData("{\"name\":\"StarServer\",\"time\":\"2026-11-11 00:00:00\",\"serverTime\":1762790400000,\"defaultLanguage\":\"auto\"}", 1762790400000L, "auto")]
    [InlineData("{\"defaultLanguage\":\"en\"}", 0L, "en")]
    [InlineData("{\"name\":\"StarServer\"}", 0L, null)]
    [InlineData("not-json", 0L, null)]
    [InlineData("", 0L, null)]
    public void Parse_Works(String json, Int64 serverTime, String? language)
    {
        var info = ServerProbe.Parse(json);

        // 时间与语言都没有时视为探测失败
        if (serverTime == 0 && language == null)
            Assert.Null(info);
        else
        {
            Assert.NotNull(info);
            Assert.Equal(serverTime, info!.ServerTime);
            Assert.Equal(language, info.DefaultLanguage);
        }
    }

    [Theory(DisplayName = "服务端地址支持逗号分隔的多个节点")]
    [InlineData("http://127.0.0.1:6600", "http://127.0.0.1:6600/api")]
    [InlineData("http://127.0.0.1:6600/", "http://127.0.0.1:6600/api")]
    [InlineData("http://a:6600,http://b:6600", "http://a:6600/api")]
    [InlineData(" http://a:6600 , http://b:6600 ", "http://a:6600/api")]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void GetApiUrl_Works(String? server, String? expected) => Assert.Equal(expected, ServerProbe.GetApiUrl(server));

    [Fact(DisplayName = "鉴权盐值跟随全局时间提供者（星尘接管 Runtime.UtcNow 后即为服务端时间）")]
    public void Salt_UsesGlobalTimeProvider()
    {
        var old = TimerScheduler.GlobalTimeProvider;
        try
        {
            var serverNow = new DateTimeOffset(2026, 11, 11, 0, 0, 0, TimeSpan.Zero);
            TimerScheduler.GlobalTimeProvider = new FixedTimeProvider(serverNow);

            var provider = new SaltPasswordProvider { Algorithm = "md5", SaltTime = 60 };
            var parts = provider.Hash("secret").Split('$');

            Assert.Equal(4, parts.Length);
            Assert.Equal("md5", parts[1]);
            Assert.Equal(serverNow.ToInt().ToString(), parts[2]);
        }
        finally
        {
            TimerScheduler.GlobalTimeProvider = old;
        }
    }

    [Fact(DisplayName = "探测失败返回空且不抛异常")]
    public void GetServerInfo_Unreachable_ReturnsNull()
    {
        using var _ = new FastProbe();

        var sw = Stopwatch.StartNew();
        var info = ServerProbe.GetServerInfo("http://127.0.0.1:1");

        Assert.Null(info);
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(3), $"探测耗时过长：{sw.Elapsed}");
    }

    [Fact(DisplayName = "失败冷却期内不重复发起探测")]
    public void GetServerInfo_WithinRetryInterval_ReturnsNull()
    {
        using var _ = new FastProbe();

        const String server = "http://127.0.0.1:2";
        Assert.Null(ServerProbe.GetServerInfo(server));

        // 等第一次请求彻底结束（连接被拒或超时），此后只应命中冷却，不应再发请求
        Thread.Sleep(800);

        var sw = Stopwatch.StartNew();
        Assert.Null(ServerProbe.GetServerInfo(server));

        Assert.True(sw.Elapsed < TimeSpan.FromMilliseconds(100), $"冷却期内应快速返回，实际 {sw.Elapsed}");
    }

    [Fact(DisplayName = "取回服务端时间与默认语言并缓存")]
    public void GetServerInfo_FromServer_ReturnsInfo()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            _ = Task.Run(() => Respond(listener, "{\"name\":\"StarServer\",\"serverTime\":1762790400000,\"defaultLanguage\":\"zh-Hans\"}"));

            var info = ServerProbe.GetServerInfo($"http://127.0.0.1:{port}");

            Assert.NotNull(info);
            Assert.Equal(1762790400000L, info!.ServerTime);
            Assert.Equal("zh-Hans", info.DefaultLanguage);

            // 探测成功后走缓存，不再发起请求
            var sw = Stopwatch.StartNew();
            var again = ServerProbe.GetServerInfo($"http://127.0.0.1:{port}");
            Assert.NotNull(again);
            Assert.True(sw.Elapsed < TimeSpan.FromMilliseconds(200), $"命中缓存应秒返回，实际 {sw.Elapsed}");
        }
        finally
        {
            listener.Stop();
        }
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _now;

        public FixedTimeProvider(DateTimeOffset now) => _now = now;

        public override DateTimeOffset GetUtcNow() => _now;
    }

    /// <summary>临时缩短探测超时，避免测试依赖本机网络环境的连接失败速度</summary>
    private sealed class FastProbe : IDisposable
    {
        private readonly TimeSpan _wait = ServerProbe.WaitTimeout;
        private readonly TimeSpan _http = ServerProbe.HttpTimeout;
        private readonly TimeSpan _retry = ServerProbe.RetryInterval;

        public FastProbe()
        {
            ServerProbe.WaitTimeout = TimeSpan.FromMilliseconds(100);
            ServerProbe.HttpTimeout = TimeSpan.FromMilliseconds(300);
            ServerProbe.RetryInterval = TimeSpan.FromMinutes(5);
        }

        public void Dispose()
        {
            ServerProbe.WaitTimeout = _wait;
            ServerProbe.HttpTimeout = _http;
            ServerProbe.RetryInterval = _retry;
        }
    }

    private static async Task Respond(TcpListener listener, String body)
    {
        using var client = await listener.AcceptTcpClientAsync().ConfigureAwait(false);
        using var stream = client.GetStream();

        // 读取请求头，避免对端收到 RST
        var buf = new Byte[1024];
        await stream.ReadAsync(buf, 0, buf.Length).ConfigureAwait(false);

        var bytes = Encoding.UTF8.GetBytes(body);
        var head = $"HTTP/1.1 200 OK\r\nContent-Type: application/json; charset=utf-8\r\nContent-Length: {bytes.Length}\r\nConnection: close\r\n\r\n";
        await stream.WriteAsync(Encoding.UTF8.GetBytes(head), 0, head.Length).ConfigureAwait(false);
        await stream.WriteAsync(bytes, 0, bytes.Length).ConfigureAwait(false);
        await stream.FlushAsync().ConfigureAwait(false);
    }
}