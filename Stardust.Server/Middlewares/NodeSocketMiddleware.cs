using System;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using NewLife;
using NewLife.Log;

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
                    var webSocket = await context.WebSockets.AcceptWebSocketAsync();
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
            else
            {
                await _next(context);
            }
        }

        private async Task Handle(WebSocket websocket)
        {
            XTrace.WriteLine("connect {0}", websocket.State);
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
    }
}