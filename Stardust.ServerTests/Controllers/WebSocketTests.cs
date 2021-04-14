using System;
using System.Net.WebSockets;
using System.Threading.Tasks;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using NewLife;
using NewLife.Security;
using Xunit;

namespace Stardust.Server.Controllers.Tests
{
    public class WebSocketTests
    {
        private readonly TestServer _server;

        public WebSocketTests()
        {
            _server = new TestServer(WebHost.CreateDefaultBuilder()
                .UseStartup<Startup>());
        }

        [Fact]
        public async Task WebSocketClient()
        {
            var client = _server.CreateWebSocketClient();
            var socket = await client.ConnectAsync(new Uri("http://localhost:6600/node_ws"), default);

            {
                var str = "hello Stone";
                await socket.SendAsync(str.GetBytes(), WebSocketMessageType.Text, true, default);

                var buf = new Byte[1024];
                var data = await socket.ReceiveAsync(buf, default);
                var rs = buf.ToStr(null, 0, data.Count);

                Assert.Equal("got " + str, rs);
            }

            {
                var str = Rand.NextString(16);
                await socket.SendAsync(str.GetBytes(), WebSocketMessageType.Text, true, default);

                var buf = new Byte[1024];
                var data = await socket.ReceiveAsync(buf, default);
                var rs = buf.ToStr(null, 0, data.Count);

                Assert.Equal("got " + str, rs);
            }
        }
    }
}