using System;
using System.Linq;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Stardust.Server;
using Xunit;

namespace ServerTest.Communication;

/// <summary>真实 Kestrel 服务端测试夹具。在随机端口启动完整 StarServer，供客户端端到端通讯测试使用</summary>
/// <remarks>
/// 与既有 TestServer（进程内管道）不同，这里监听真实 TCP 端口，
/// 让 StarClient/AppClient 走完整网络链路（套接字、HTTP 头令牌、JSON 报文、WebSocket 长连接）。
/// </remarks>
public sealed class StarServerFixture : IDisposable
{
    private readonly IWebHost _host;

    /// <summary>服务端根地址，例如 http://127.0.0.1:54321</summary>
    public String ServerUrl { get; }

    /// <summary>服务端宿主，可用于解析服务容器中的单例服务</summary>
    public IWebHost Host => _host;

    public StarServerFixture()
    {
        // 测试进程自己的星尘客户端不要去连开发者本机正在跑的 6600，否则和夹具抢同一份库。
        Environment.SetEnvironmentVariable("StarServer", "http://127.0.0.1:1");
        // 节点登录会按 IP 库解析地区。库不在时会同步去 PluginServer 下载，超过客户端滚动超时后登录被取消。
        EnsureIpDatabase();

        // 关闭 SQL 与调试日志，并停用 .NET 运行时信息同步，避免测试期间写大量日志与访问外网
        StarTestServer.DisableDebugLog();
        XCode.XCodeSetting.Current.ShowSQL = false;
        XCode.XCodeSetting.Current.Debug = false;

#pragma warning disable CS0618, ASPDEPR008
        _host = WebHost.CreateDefaultBuilder()
            .UseStartup<Startup>()
            .UseUrls("http://127.0.0.1:0")
            .Build();
#pragma warning restore CS0618, ASPDEPR008

        if (_host.Services.GetService(typeof(StarServerSetting)) is StarServerSetting set)
        {
            set.DotNetSyncPeriod = 0;
            set.DotNetSyncUrl = "";
        }

        _host.Start();

        var address = _host.ServerFeatures.Get<IServerAddressesFeature>()?.Addresses?.FirstOrDefault();
        ServerUrl = address?.TrimEnd('/') ?? throw new InvalidOperationException("无法获取 StarServer 服务端监听地址");
    }

    /// <summary>销毁服务端</summary>
    public void Dispose() => _host.Dispose();

    /// <summary>节点登录解析地区时读取 Data/ip.gz。缺文件会阻塞登录去下载，这里复用本机已有库。</summary>
    private static void EnsureIpDatabase()
    {
        var dstDir = Path.Combine(AppContext.BaseDirectory, "Data");
        Directory.CreateDirectory(dstDir);
        var dst = Path.Combine(dstDir, "ip.gz");
        if (File.Exists(dst)) return;

        var src = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "Data", "ip.gz"));
        if (File.Exists(src)) File.Copy(src, dst);
    }
}

/// <summary>共享真实 StarServer 的测试集合</summary>
[CollectionDefinition("StarServer")]
public class StarServerCollection : ICollectionFixture<StarServerFixture>
{
}
