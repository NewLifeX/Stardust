using System;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using NewLife;
using NewLife.Caching;
using NewLife.Log;
using NewLife.Remoting;
using NewLife.Web;
using Stardust.Data.Nodes;

namespace Stardust.Server.Middlewares
{
    public class NodeSocketMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ICache _cache;

        /// <summary>实例化</summary>
        /// <param name="next"></param>
        public NodeSocketMiddleware(RequestDelegate next, ICache cache)
        {
            _next = next ?? throw new ArgumentNullException(nameof(next));
            _cache = cache;
        }

        /// <summary>调用</summary>
        /// <param name="ctx"></param>
        /// <returns></returns>
        public async Task Invoke(HttpContext context)
        {
            if (context.Request.Path == "/test_ws")
            {
                if (context.WebSockets.IsWebSocketRequest)
                {
                    using var webSocket = await context.WebSockets.AcceptWebSocketAsync();
                    try
                    {
                        await Handle(webSocket);
                    }
                    catch (Exception ex)
                    {
                        XTrace.WriteException(ex);

                        await context.Response.WriteAsync("websocket closed");
                    }
                }
                else
                {
                    context.Response.StatusCode = 404;
                }
            }
            else if (context.Request.Path == "/node_ws")
            {
                if (context.WebSockets.IsWebSocketRequest)
                {
                    var token = (context.Request.Headers["Authorization"] + "").TrimStart("Bearer ");
                    var webSocket = await context.WebSockets.AcceptWebSocketAsync();
                    var source = new CancellationTokenSource();
                    try
                    {
                        await Handle(webSocket, token, source.Token);
                    }
                    catch (Exception ex)
                    {
                        source.Cancel();
                        await webSocket.CloseAsync(WebSocketCloseStatus.InternalServerError, ex.Message, default);
                    }
                }
                else
                {
                    context.Response.StatusCode = 404;
                }
            }
            else
            {
                await _next(context);
            }
        }

        private async Task Handle(WebSocket websocket)
        {
            while (true)
            {
                var buffer = new Byte[1024 * 1];
                var data = await websocket.ReceiveAsync(new ArraySegment<Byte>(buffer), CancellationToken.None);
                if (data.CloseStatus.HasValue) break;

                if (data.MessageType == WebSocketMessageType.Text)
                {
                    var str = buffer.ToStr(null, 0, data.Count);
                    XTrace.WriteLine("receive {0}", str);

                    await websocket.SendAsync(("got " + str).GetBytes(), WebSocketMessageType.Text, true, default);
                }
            }
        }

        private async Task Handle(WebSocket websocket, String token, CancellationToken cancellationToken)
        {
            var node = DecodeToken(token, Setting.Current.TokenSecret);
            if (node == null) throw new InvalidOperationException("未登录！");

            XTrace.WriteLine("websocket连接/node_ws {0}", node);

            var queue = _cache.GetQueue<String>($"cmd:{node.Code}");
            while (!cancellationToken.IsCancellationRequested && websocket.State == WebSocketState.Open)
            {
                var msg = await queue.TakeOneAsync(10_000);
                if (msg != null)
                {
                    await websocket.SendAsync(msg.GetBytes(), WebSocketMessageType.Text, true, cancellationToken);
                }
                else
                {
                    // 后续MemoryQueue升级到异步阻塞版以后，这里可以缩小
                    await Task.Delay(1_000);
                }
            }

            XTrace.WriteLine("websocket关闭/node_ws {0}", node);

            await websocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "finish", cancellationToken);
        }

        private Node DecodeToken(String token, String tokenSecret)
        {
            if (token.IsNullOrEmpty()) throw new ApiException(402, "节点未登录");

            // 解码令牌
            var ss = tokenSecret.Split(':');
            var jwt = new JwtBuilder
            {
                Algorithm = ss[0],
                Secret = ss[1],
            };

            var rs = jwt.TryDecode(token, out var message);
            var node = Node.FindByCode(jwt.Subject);
            if (!rs) throw new ApiException(403, $"非法访问 {message}");

            return node;
        }
    }
}