using NewLife.Caching;
using NewLife.Configuration;
using NewLife.Model;
using Stardust;
using Xunit;

namespace ClientTest;

public class ConfigTests
{
    [Fact]
    public void Http_Test()
    {
        {
            var prv = new HttpConfigProvider
            {
                Server = "http://127.0.0.1:6600",
                AppId = "StarWeb"
            };

            var title = prv["Title"];
            Assert.Equal("NewLife开发团队", title);

            var shop = prv["conn_shop"];
            Assert.Equal("server=10.0.0.1;user=maindb;pass=Pass@word", shop);
        }
        {
            var prv = new HttpConfigProvider
            {
                Server = "http://127.0.0.1:6600",
                AppId = "StarWeb",
                Scope = "dev",
            };

            var title = prv["Title"];
            Assert.Equal("NewLife开发团队", title);

            var shop = prv["conn_shop"];
            Assert.Equal("server=192.168.0.1;user=dev;pass=dev1234", shop);
        }
    }

    [Fact]
    public void Redis_ConfigTest()
    {
        using var star = new StarFactory("http://star.newlifex.com:6600", "Test", null);

        var services = ObjectContainer.Current;
        services.AddSingleton(star.Config);

        services.AddSingleton(p => new Redis(p, "redis6"));

        var provider = services.BuildServiceProvider();

        var rds = provider.GetService<Redis>();
        Assert.Equal(6, rds.Db);
    }
}