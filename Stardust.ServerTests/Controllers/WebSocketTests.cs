using System;
using System.Net.WebSockets;
using System.Threading.Tasks;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using NewLife;
using NewLife.Log;
using NewLife.Remoting;
using NewLife.Security;
using NewLife.Serialization;
using Stardust.Models;
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

        [Fact]
        public async Task WebSocketClient2()
        {
            var http = _server.CreateClient();
            var rs = await http.PostAsync<LoginResponse>("node/login", new
            {
                code = "",
                node = new
                {
                    MachineName = "test",
                    macs = "xxyyzz"
                }
            });
            XTrace.WriteLine(rs.ToJson());

            var client = _server.CreateWebSocketClient();
            client.ConfigureRequest = q => { q.Headers.Add("Authorization", "Bearer " + rs.Token); };
            var socket = await client.ConnectAsync(new Uri("http://localhost:6600/node_ws"), default);

            for (var i = 0; i < 3; i++)
            {
                var buf = new Byte[1024];
                var data = await socket.ReceiveAsync(buf, default);
                var rs2 = buf.ToStr(null, 0, data.Count);

                Assert.Equal(rs.Code + "-" + (i + 1), rs2);
            }
        }
    }
}