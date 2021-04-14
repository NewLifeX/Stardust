using System;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using NewLife;
using NewLife.Log;
using NewLife.Remoting;
using NewLife.Web;
using Stardust.Data.Nodes;

namespace Stardust.Server.Middlewares
{
    public class NodeSocketMiddleware
    {
        private readonly RequestDelegate _next;

        /// <summary>实例化</summary>
        /// <param name="next"></param>
        public NodeSocketMiddleware(RequestDelegate next) => _next = next ?? throw new ArgumentNullException(nameof(next));

        /// <summary>调用</summary>
        /// <param name="ctx"></param>
        /// <returns></returns>
        public async Task Invoke(HttpContext context)
        {
            if (context.Request.Path == "/node_ws")
            {
                if (context.WebSockets.IsWebSocketRequest)
                {
                    var token = (context.Request.Headers["Authorization"] + "").TrimStart("Bearer ");
                    var webSocket = await context.WebSockets.AcceptWebSocketAsync();
                    try
                    {
                        await Handle(webSocket, token);
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
            else
            {
                await _next(context);
            }
        }

        private async Task Handle(WebSocket websocket, String token)
        {
            var node = DecodeToken(token, Setting.Current.TokenSecret);
            XTrace.WriteLine("connect {0} {1}", node, websocket.State);

            while (true)
            {
                if (node != null)
                {
                    for (var i = 0; i < 3; i++)
                    {
                        //var str = "message" + (i + 1);
                        var str = node.Code + "-" + (i + 1);
                        await websocket.SendAsync(str.GetBytes(), WebSocketMessageType.Text, true, default);

                        Thread.Sleep(500);
                    }
                }
                else
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
        }

        private Node DecodeToken(String token, String tokenSecret)
        {
            //if (token.IsNullOrEmpty()) throw new ArgumentNullException(nameof(token));
            //if (token.IsNullOrEmpty()) throw new ApiException(402, "节点未登录");
            if (token.IsNullOrEmpty()) return null;

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