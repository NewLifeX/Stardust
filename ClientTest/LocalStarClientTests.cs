using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Stardust;
using Xunit;

namespace ClientTest
{
    public class LocalStarClientTests
    {
        [Fact]
        public void Info()
        {
            var client = new LocalStarClient();
            var inf = client.GetInfo();

            Assert.NotNull(inf);
            Assert.NotEmpty(inf.Server);
        }
    }
}
