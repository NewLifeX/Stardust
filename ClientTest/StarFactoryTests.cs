using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NewLife;
using NewLife.Configuration;
using NewLife.Http;
using NewLife.Log;
using NewLife.Model;
using NewLife.Reflection;
using NewLife.Remoting;
using NewLife.Security;
using Stardust;
using Stardust.Monitors;
using Xunit;

namespace ClientTest
{
    public class StarFactoryTests
    {
        [Fact]
        public void Normal()
        {
            var set = Stardust.Setting.Current;
            var secret = Rand.NextString(8, true);
            set.Secret = secret;

            using var star = new StarFactory(null, "StarWeb", null);

            Assert.NotNull(star.Local);
            Assert.Equal("http://star.newlifex.com:6600", star.Server);
            Assert.Equal("StarWeb", star.AppId);
            Assert.Equal(secret, star.Secret);

            var inf = star.Local.Info;
            Assert.NotNull(inf);

            var tracer = star.Tracer as StarTracer;
            Assert.NotNull(tracer);
            Assert.NotEmpty(tracer.ClientId);

            var config = star.Config as HttpConfigProvider;
            Assert.NotNull(config);
            Assert.Equal("NewLife开发团队", config["Title"]);

            var dust = star.Service;
            Assert.NotNull(dust);

            var filter = star.GetValue("_tokenFilter") as TokenHttpFilter;
            Assert.NotNull(filter);
            Assert.Equal(star.AppId, filter.UserName);
            Assert.Equal(star.Secret, filter.Password);
            Assert.Equal(filter, (tracer.Client as ApiHttpClient).Filter);
            Assert.Equal(filter, (config.Client as ApiHttpClient).Filter);
            Assert.Equal(filter, (dust.Client as ApiHttpClient).Filter);
        }

        [Fact]
        public async void CreateForService()
        {
            using var star = new StarFactory("http://127.0.0.1:6600", "test", "xxx");
            await star.Service.RegisterAsync("testService", "http://localhost:1234", "tA,tagB,ttC");

            var client = star.CreateForService("testService", "tagB") as ApiHttpClient;
            Assert.NotNull(client);
            Assert.True(client.RoundRobin);
            Assert.Equal("http://localhost:1234/", client.Services.Join(",", e => e.Address));
        }

        [Fact]
        public void IocTest()
        {
            using var star = new StarFactory("http://127.0.0.1:6600", "test", null);

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

            var service = provider.GetRequiredService<DustClient>();
            Assert.NotNull(service);
            Assert.Equal(star.Service, service);
        }

        [Fact]
        public async void SendNodeCommand()
        {
            using var star = new StarFactory("http://127.0.0.1:6600", "test", "xxx");

            var rs = await star.SendNodeCommand("7F0F011A", "hello", "stone", DateTime.Now.AddMinutes(33));
            Assert.True(rs > 0);
        }
    }
}
