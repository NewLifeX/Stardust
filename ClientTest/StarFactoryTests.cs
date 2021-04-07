using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NewLife.Configuration;
using NewLife.Http;
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
            var set = Setting.Current;
            var secret = Rand.NextString(8, true);
            set.Secret = secret;

            var star = new StarFactory(null, "StarWeb", null);

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

            var dust = star.Dust;
            Assert.NotNull(dust);

            var filter = star.GetValue("_tokenFilter") as TokenHttpFilter;
            Assert.NotNull(filter);
            Assert.Equal(star.AppId, filter.UserName);
            Assert.Equal(star.Secret, filter.Password);
            Assert.Equal(filter, (tracer.Client as ApiHttpClient).Filter);
            Assert.Equal(filter, (config.Client as ApiHttpClient).Filter);
            Assert.Equal(filter, (dust.Client as ApiHttpClient).Filter);
        }
    }
}
