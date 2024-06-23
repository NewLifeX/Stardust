using System;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using NewLife;
using NewLife.Caching;
using NewLife.Log;
using NewLife.Model;
using NewLife.Remoting;
using NewLife.Remoting.Models;
using NewLife.Security;
using NewLife.Serialization;
using NewLife.Threading;
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

        [Fact(Skip = "跳过")]
        public async Task WebSocketClient()
        {
            var client = _server.CreateWebSocketClient();
            var socket = await client.ConnectAsync(new Uri("http://localhost:6600/test_ws"), default);

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
            client.ConfigureRequest = q => { q.Headers.Append("Authorization", "Bearer " + rs.Token); };
            using var socket = await client.ConnectAsync(new Uri("http://localhost:6600/node/notify"), default);

            var ms = new[] { "开灯", "关门", "吃饭" };

            // 模拟推送指令到队列
            ThreadPool.QueueUserWorkItem(s =>
            {
                var cache = _server.Services.GetRequiredService<ICache>();
                var queue = cache.GetQueue<String>($"cmd:{rs.Code}");
                for (var j = 0; j < 3; j++)
                {
                    Thread.Sleep(500);

                    var msg = $"{ms[j]}";
                    XTrace.WriteLine("Add Command: {0}", msg);
                    queue.Add(msg);
                }
            });

            // 客户端接收服务端推送的指令
            for (var i = 0; i < 3; i++)
            {
                var buf = new Byte[1024];
                var data = await socket.ReceiveAsync(buf, default);
                var cmd = buf.ToStr(null, 0, data.Count);

                XTrace.WriteLine("Got Command: {0}", cmd);

                Assert.Equal($"{ms[i]}", cmd);
            }

            //await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "end", default);
            socket.Dispose();
        }
    }
}