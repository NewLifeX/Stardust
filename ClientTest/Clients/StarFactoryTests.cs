using System.Threading.Tasks;
using NewLife.Configuration;
using NewLife.Log;
using NewLife.Model;
using NewLife.Remoting;
using NewLife.Security;
using Stardust;
using Stardust.Monitors;
using Stardust.Registry;
using Xunit;

namespace ClientTest.Clients;

public class StarFactoryTests
{
    [Fact]
    public void Normal()
    {
        var set = StarSetting.Current;
        var secret = Rand.NextString(8, true);
        set.Secret = secret;

        // 环境变量优先于 appsettings 与本地 star.config，固定为测试地址，避免受其它测试写入的配置影响
        var old = Environment.GetEnvironmentVariable("StarServer");
        Environment.SetEnvironmentVariable("StarServer", "http://127.0.0.1:6600");
        try
        {
            using var star = new StarFactory(null, "StarWeb", null);

            Assert.NotNull(star.Local);
            Assert.Equal("http://127.0.0.1:6600", star.Server);
            Assert.Equal("StarWeb", star.AppId);
            Assert.Equal(secret, star.Secret);

            var inf = star.Local.Info;
            // CI 无本机 StarAgent 时 Info 为空，跳过本地探测相关断言
            if (inf != null) Assert.NotEmpty(inf.Server);

            // 服务端不可达时，Tracer 初始化会触发客户端登录重试，耗时数秒且无法完成断言，直接跳过联网部分
            if (!TestEnvironment.CanGet("http://127.0.0.1:6600")) return;

            var tracer = star.Tracer as StarTracer;
            Assert.NotNull(tracer);
            Assert.NotEmpty(tracer.ClientId);

            var config = star.Config as HttpConfigProvider;
            Assert.NotNull(config);
            Assert.Equal("NewLife开发团队", config["Title"]);

            var dust = star.Service as AppClient;
            Assert.NotNull(dust);

            //var filter = star.GetValue("_tokenFilter") as TokenHttpFilter;
            //Assert.NotNull(filter);
            //Assert.Equal(star.AppId, filter.UserName);
            //Assert.Equal(star.Secret, filter.Password);
            //Assert.Equal(filter, (tracer.Client as ApiHttpClient).Filter);
            //Assert.Equal(filter, (config.Client as ApiHttpClient).Filter);
            //Assert.Equal(filter, (dust as ApiHttpClient).Filter);
        }
        finally
        {
            Environment.SetEnvironmentVariable("StarServer", old);
        }
    }

    [Fact]
    public async Task CreateForService()
    {
        if (!await TestEnvironment.CanGetAsync("http://127.0.0.1:6600")) return;

        using var star = new StarFactory("http://127.0.0.1:6600", "test", "xxx");
        await star.Service.RegisterAsync("testService", "http://localhost:1234", "tA,tagB,ttC");

        var client = star.Service!.GetClient("testService", "tagB") as ApiHttpClient;
        Assert.NotNull(client);
        Assert.Equal(LoadBalanceMode.RoundRobin, client.LoadBalanceMode);
        //Assert.Equal("http://localhost:1234/", client.Services.Join(",", e => e.Address));
        Assert.Contains(client.Services, e => e.Address + "" == "http://localhost:1234/");
    }

    [Fact]
    public void CreateForService2()
    {
        // 服务端不可达时 GetClient 会等待登录重试超时，直接跳过联网断言
        if (!TestEnvironment.CanGet("http://127.0.0.1:6600")) return;

        using var star = new StarFactory("http://127.0.0.1:6600", "test", "xxx");

        var client = star.Service!.GetClient("StarWeb", "tagB") as ApiHttpClient;
        Assert.NotNull(client);
        Assert.Equal(LoadBalanceMode.RoundRobin, client.LoadBalanceMode);
        Assert.All(client.Services, e => Assert.False(String.IsNullOrEmpty(e.Address + "")));

        // 第二次请求，避免使用前面的缓存
        var client2 = star.Service!.GetClient("StarWeb", null) as ApiHttpClient;
        Assert.NotNull(client2);
        Assert.Equal(LoadBalanceMode.RoundRobin, client2.LoadBalanceMode);
        //Assert.Equal("https://localhost:5001/,http://localhost:5000/", client2.Services.Join(",", e => e.Address));
        Assert.NotEmpty(client2.Services);
    }

    [Fact]
    public void IocTest()
    {
        using var star = new StarFactory("http://127.0.0.1:6600", "test", null);
        star.Register(ObjectContainer.Current);

        var provider = ObjectContainer.Provider;

        var factory = provider.GetRequiredService<StarFactory>();
        Assert.NotNull(factory);
        Assert.Equal(star, factory);

        var tracer = provider.GetRequiredService<ITracer>();
        Assert.NotNull(tracer);
        Assert.Equal(star.Tracer, tracer);

        var config = provider.GetRequiredService<IConfigProvider>();
        Assert.NotNull(config);
        Assert.Equal(star.Config, config);

        var service = provider.GetRequiredService<IRegistry>();
        Assert.NotNull(service);
        Assert.Equal(star.Service, service);
    }

    [Fact]
    public async Task SendNodeCommand()
    {
        if (!await TestEnvironment.CanGetAsync("http://127.0.0.1:6600")) return;

        using var star = new StarFactory("http://127.0.0.1:6600", "StarWeb", "xxx");

        var rs = await star.SendNodeCommandAsync("81AFCC68", "hello", "stone", 33, cancellationToken: default);
        Assert.NotNull(rs);
    }
}
