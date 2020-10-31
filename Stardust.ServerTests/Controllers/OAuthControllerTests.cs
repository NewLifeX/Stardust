using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using NewLife.Remoting;
using Stardust.Models;
using Stardust.Server.Models;
using Xunit;

namespace Stardust.Server.Controllers.Tests
{
    public class OAuthControllerTests
    {
        private readonly TestServer _server;

        public OAuthControllerTests()
        {
            _server = new TestServer(WebHost.CreateDefaultBuilder()
                .UseStartup<Startup>());
        }

        [Fact]
        public async void Token_password()
        {
            var model = new TokenInModel
            {
                grant_type = "password",
                UserName = "stone",
            };

            var client = _server.CreateClient();

            var rs = await client.GetAsync<TokenModel>("oauth/token", model);
            Assert.NotNull(rs);
            Assert.NotEmpty(rs.AccessToken);
        }
    }
}