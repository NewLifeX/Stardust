using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using NewLife.Configuration;
using NewLife.Serialization;
using Stardust.Configs;
using Xunit;

namespace Stardust.Tests;

/// <summary>HTTP 配置提供者测试。用本地配置中心桩验证配置拉取与 ConfigInfo 解析（SDK-3 / CFG-7）</summary>
public class StarHttpConfigProviderTests
{
    /// <summary>极简配置中心桩：任意请求都返回 {code:0,data:{configs,version}}</summary>
    private sealed class ConfigServerStub : IDisposable
    {
        private readonly HttpListener _listener = new();

        /// <summary>服务地址，如 http://127.0.0.1:12345</summary>
        public String Server { get; }

        /// <summary>返回给客户端的配置集合</summary>
        public Dictionary<String, Object?> Configs { get; } = [];

        /// <summary>配置版本号</summary>
        public Int32 Version { get; set; } = 1;

        public ConfigServerStub()
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

                _ = Task.Run(() => Respond(ctx));
            }
        }

        private void Respond(HttpListenerContext ctx)
        {
            try
            {
                var data = new Dictionary<String, Object?>
                {
                    ["appId"] = "cfgapp",
                    ["version"] = Version,
                    ["configs"] = Configs,
                };
                var json = new Dictionary<String, Object?> { ["code"] = 0, ["data"] = data }.ToJson();
                var buf = Encoding.UTF8.GetBytes(json);

                ctx.Response.ContentType = "application/json";
                ctx.Response.ContentLength64 = buf.Length;
                ctx.Response.OutputStream.Write(buf, 0, buf.Length);
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

    [Fact(DisplayName = "拉取配置后可按键读取，并解析出 ConfigInfo 版本号")]
    public void LoadAll_ParsesConfigInfo()
    {
        using var stub = new ConfigServerStub();
        stub.Configs["foo"] = "bar";
        stub.Configs["NewLife.Debug"] = "true";
        stub.Version = 7;

        using var provider = new StarHttpConfigProvider
        {
            Server = stub.Server,
            AppId = "cfgapp",
            CacheLevel = ConfigCacheLevel.NoCache,
            Period = 0,
        };

        Assert.True(provider.LoadAll());

        Assert.Equal("bar", provider["foo"]);
        Assert.NotNull(provider.ConfigInfo);
        Assert.Equal(7, provider.ConfigInfo!.Version);
        Assert.Equal("bar", provider.ConfigInfo.Configs!["foo"]);
    }

    [Fact(DisplayName = "配置中心返回新版本时同步更新 ConfigInfo")]
    public void LoadAll_RefreshesVersion()
    {
        using var stub = new ConfigServerStub();
        stub.Configs["foo"] = "v1";
        stub.Version = 1;

        using var provider = new StarHttpConfigProvider
        {
            Server = stub.Server,
            AppId = "cfgapp",
            CacheLevel = ConfigCacheLevel.NoCache,
            Period = 0,
        };

        Assert.True(provider.LoadAll());
        Assert.Equal("v1", provider["foo"]);

        stub.Configs["foo"] = "v2";
        stub.Version = 2;

        Assert.True(provider.LoadAll());
        Assert.Equal("v2", provider["foo"]);
        Assert.Equal(2, provider.ConfigInfo!.Version);
    }

    [Fact(DisplayName = "配置中心不可达时加载失败但不抛异常")]
    public void LoadAll_Unreachable_NoThrow()
    {
        using var provider = new StarHttpConfigProvider
        {
            Server = "http://127.0.0.1:9",
            AppId = "cfgapp",
            CacheLevel = ConfigCacheLevel.NoCache,
            Period = 0,
        };

        var rs = provider.LoadAll();

        Assert.False(rs);
    }
}
