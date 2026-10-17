using System;
using System.IO;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Stardust.Server;
using Xunit;

namespace ServerTest;

/// <summary>
/// 进程内 StarServer 测试夹具。整个测试程序集共用同一个 TestServer 实例。
/// </summary>
/// <remarks>
/// 早期每个测试类（甚至每个测试方法）都在构造函数里新建 <see cref="TestServer"/>，
/// 一次完整测试会重复启动三百多次主机并重建全部数据表，成为单元测试的主要耗时来源。
/// 这里改为集合夹具，全程序集只启动一次；数据库与静态配置本来就由各测试共享，语义不变。
/// 同时关闭 XCode 的 SQL 日志与调试日志，并停用 .NET 运行时信息同步，避免测试期间访问外网。
/// </remarks>
public sealed class StarTestServer : IDisposable
{
    /// <summary>共享的进程内测试服务器</summary>
    public TestServer Server { get; }

    public StarTestServer()
    {
        // 关闭 SQL 与调试日志，显著减少日志落盘与字符串拼接开销
        DisableDebugLog();
        XCode.XCodeSetting.Current.ShowSQL = false;
        XCode.XCodeSetting.Current.Debug = false;

        var host = BuildHost();
        Server = host.GetTestServer();
    }

    /// <summary>把 XCode 配置文件改成关闭调试与 SQL 日志。XCode 启动时会重新读取该文件，仅靠内存赋值不可靠</summary>
    internal static void DisableDebugLog()
    {
        try
        {
            var dir = Path.Combine(AppContext.BaseDirectory, "Config");
            Directory.CreateDirectory(dir);

            var file = Path.Combine(dir, "XCode.config");
            if (File.Exists(file))
            {
                var xml = File.ReadAllText(file);
                var changed = false;
                if (xml.Contains("<ShowSQL>true</ShowSQL>"))
                {
                    xml = xml.Replace("<ShowSQL>true</ShowSQL>", "<ShowSQL>false</ShowSQL>");
                    changed = true;
                }
                if (xml.Contains("<Debug>true</Debug>"))
                {
                    xml = xml.Replace("<Debug>true</Debug>", "<Debug>false</Debug>");
                    changed = true;
                }
                if (changed) File.WriteAllText(file, xml);
            }
            else
            {
                File.WriteAllText(file, "<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n<XCode>\r\n  <Debug>false</Debug>\r\n  <ShowSQL>false</ShowSQL>\r\n</XCode>\r\n");
            }
        }
        catch
        {
            // 配置文件不可写时退回到内存赋值
        }
    }

    private static IWebHost BuildHost()
    {
#pragma warning disable CS0618, ASPDEPR008
        var host = WebHost.CreateDefaultBuilder()
            .UseStartup<Startup>()
            .UseTestServer()
            .Build();
#pragma warning restore CS0618, ASPDEPR008

        // .NET 运行时信息同步会访问外网且带 30 秒超时，测试环境必须关闭
        var set = host.Services.GetService(typeof(StarServerSetting)) as StarServerSetting;
        if (set != null)
        {
            set.DotNetSyncPeriod = 0;
            set.DotNetSyncUrl = "";
        }

        host.Start();

        return host;
    }

    /// <summary>销毁测试服务器</summary>
    public void Dispose() => Server.Dispose();
}

/// <summary>共享进程内 StarServer 的测试集合</summary>
[CollectionDefinition(Name)]
public class StarTestServerCollection : ICollectionFixture<StarTestServer>
{
    /// <summary>集合名。测试类用 [Collection(StarTestServerCollection.Name)] 声明</summary>
    public const String Name = "StarTestServer";
}

/// <summary>依赖共享 TestServer 的测试基类</summary>
/// <remarks>测试类继承本基类即可使用 <see cref="_server"/>，无需在每个测试方法中重复启动主机</remarks>
[Collection(StarTestServerCollection.Name)]
public abstract class StarTestBase
{
    private protected readonly TestServer _server;

    /// <summary>实例化测试基类</summary>
    /// <param name="fixture">共享测试服务器夹具</param>
    protected StarTestBase(StarTestServer fixture) => _server = fixture.Server;
}
