using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using NewLife;
using NewLife.Caching;
using NewLife.Data;
using NewLife.Log;
using NewLife.Remoting;
using NewLife.Serialization;
using Stardust.Data;
using Stardust.Data.Configs;
using Stardust.Models;
using Stardust.Server.Common;
using Stardust.Server.Models;
using Stardust.Server.Services;

namespace Stardust.Server.Controllers
{
    /// <summary>应用接口控制器</summary>
    [Route("[controller]")]
    public class AppController : ControllerBase
    {
        /// <summary>用户主机</summary>
        public String UserHost => HttpContext.GetUserHost();

        private readonly TokenService _tokenService;
        private static readonly ICache _cache = new MemoryCache();
        private readonly ICache _queue;

        public AppController(ICache queue, TokenService tokenService)
        {
            _queue = queue;
            _tokenService = tokenService;
        }

        #region 心跳
        [ApiFilter]
        [HttpPost(nameof(Ping))]
        public PingResponse Ping(AppInfo inf, String token)
        {
            var rs = new PingResponse
            {
                //Time = inf.Time,
                ServerTime = DateTime.UtcNow,
            };

            var app = _tokenService.DecodeToken(token, Setting.Current.TokenSecret);
            if (app != null)
            {
                var ip = UserHost;

                app.UpdateIP = ip;
                app.SaveAsync();

                //rs.Period = app.Period;

                if (!inf.ClientId.IsNullOrEmpty())
                {
                    var olt = AppOnline.GetOrAdd(inf.ClientId);
                    olt.Name = app.Name;
                    olt.Category = app.Category;
                    olt.Version = inf.Version;
                    olt.Token = token;
                    olt.PingCount++;
                    if (olt.CreateIP.IsNullOrEmpty()) olt.CreateIP = ip;
                    olt.Creator = Environment.MachineName;

                    //olt.Save(null, inf, token, ip);
                    olt.Fill(app, inf);
                    olt.SaveAsync();
                }
            }

            return rs;
        }

        [ApiFilter]
        [HttpGet(nameof(Ping))]
        public PingResponse Ping()
        {
            return new PingResponse
            {
                Time = 0,
                ServerTime = DateTime.Now,
            };
        }
        #endregion

        #region 下行通知
        /// <summary>下行通知</summary>
        /// <returns></returns>
        [HttpGet("/app/notify")]
        public async Task Notify()
        {
            if (HttpContext.WebSockets.IsWebSocketRequest)
            {
                var token = (HttpContext.Request.Headers["Authorization"] + "").TrimStart("Bearer ");
                using var socket = await HttpContext.WebSockets.AcceptWebSocketAsync();

                await Handle(socket, token);
            }
            else
            {
                HttpContext.Response.StatusCode = 400;
            }
        }

        private async Task Handle(WebSocket socket, String token)
        {
            var app = _tokenService.DecodeToken(token, Setting.Current.TokenSecret);
            if (app == null) throw new InvalidOperationException("未登录！");

            XTrace.WriteLine("WebSocket连接 {0}", app);
            WriteHistory(app, "WebSocket连接", true, socket.State + "");

            var source = new CancellationTokenSource();
            _ = Task.Run(() => consumeMessage(socket, app, source));
            try
            {
                var buf = new Byte[4 * 1024];
                while (socket.State == WebSocketState.Open)
                {
                    var data = await socket.ReceiveAsync(new ArraySegment<Byte>(buf), default);
                    if (data.MessageType == WebSocketMessageType.Close) break;
                    if (data.MessageType == WebSocketMessageType.Text)
                    {
                        var str = buf.ToStr(null, 0, data.Count);
                        XTrace.WriteLine("WebSocket接收 {0} {1}", app, str);
                        WriteHistory(app, "WebSocket接收", true, str);
                    }
                }

                source.Cancel();
                XTrace.WriteLine("WebSocket断开 {0}", app);
                WriteHistory(app, "WebSocket断开", true, socket.State + "");

                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "finish", default);
            }
            catch (WebSocketException ex)
            {
                XTrace.WriteLine("WebSocket异常 {0}", app);
                XTrace.WriteLine(ex.Message);
            }
            finally
            {
                source.Cancel();
            }
        }

        private async Task consumeMessage(WebSocket socket, App app, CancellationTokenSource source)
        {
            var cancellationToken = source.Token;
            var queue = _queue.GetQueue<String>($"appcmd:{app.Name}");
            try
            {
                while (!cancellationToken.IsCancellationRequested && socket.State == WebSocketState.Open)
                {
                    var msg = await queue.TakeOneAsync(10_000);
                    if (msg != null)
                    {
                        XTrace.WriteLine("WebSocket发送 {0} {1}", app, msg);
                        WriteHistory(app, "WebSocket发送", true, msg);

                        await socket.SendAsync(msg.GetBytes(), WebSocketMessageType.Text, true, cancellationToken);
                    }
                    else
                    {
                        await Task.Delay(100, cancellationToken);
                    }
                }
            }
            catch (TaskCanceledException) { }
            catch (Exception ex)
            {
                XTrace.WriteException(ex);
            }
            finally
            {
                source.Cancel();
            }
        }

        /// <summary>向节点发送命令</summary>
        /// <param name="model"></param>
        /// <param name="token">应用令牌</param>
        /// <returns></returns>
        [ApiFilter]
        [HttpPost(nameof(SendCommand))]
        public Int32 SendCommand(CommandInModel model, String token)
        {
            if (model.Code.IsNullOrEmpty()) throw new ArgumentNullException(nameof(model.Code), "必须指定应用");
            if (model.Command.IsNullOrEmpty()) throw new ArgumentNullException(nameof(model.Command));

            var node = App.FindByName(model.Code);
            if (node == null) throw new ArgumentOutOfRangeException(nameof(model.Code), "无效应用");

            var app = _tokenService.DecodeToken(token, Setting.Current.TokenSecret);
            if (app == null || app.AllowControlNodes.IsNullOrEmpty()) throw new InvalidOperationException("无权操作！");

            if (app.AllowControlNodes != "*" && !node.Name.EqualIgnoreCase(app.AllowControlNodes.Split(",")))
                throw new InvalidOperationException($"[{app}]无权操作应用[{node}]！");

            var cmd = new AppCommand
            {
                AppId = node.Id,
                Command = model.Command,
                Argument = model.Argument,
                //Expire = model.Expire,

                CreateUser = app.Name,
            };
            if (model.Expire > 0) cmd.Expire = DateTime.Now.AddSeconds(model.Expire);
            cmd.Insert();

            var queue = _queue.GetQueue<String>($"appcmd:{node.Name}");
            queue.Add(cmd.ToModel().ToJson());

            return cmd.Id;
        }

        /// <summary>设备端响应服务调用</summary>
        /// <param name="model">服务</param>
        /// <returns></returns>
        [ApiFilter]
        [HttpPost(nameof(CommandReply))]
        public Int32 CommandReply(CommandReplyModel model, String token)
        {
            var node = _tokenService.DecodeToken(token, Setting.Current.TokenSecret);
            if (node == null) throw new ApiException(402, "节点未登录");

            var cmd = AppCommand.FindById(model.Id);
            if (cmd == null) return 0;

            cmd.Status = model.Status;
            cmd.Result = model.Data;
            cmd.Update();

            return 1;
        }
        #endregion

        #region 发布、消费
        private Service GetService(String serviceName)
        {
            var info = Service.FindByName(serviceName);
            if (info == null)
            {
                info = new Service { Name = serviceName, Enable = true };
                info.Insert();
            }
            if (!info.Enable) throw new InvalidOperationException($"服务[{serviceName}]已停用！");

            return info;
        }

        [ApiFilter]
        [HttpPost]
        public AppService RegisterService([FromBody] PublishServiceInfo service, String token)
        {
            var app = _tokenService.DecodeToken(token, Setting.Current.TokenSecret);
            var info = GetService(service.ServiceName);

            // 所有服务
            var services = AppService.FindAllByService(info.Id);
            var svc = services.FirstOrDefault(e => e.AppId == app.Id && e.Client == service.ClientId);
            if (svc == null)
            {
                svc = new AppService
                {
                    AppId = app.Id,
                    ServiceId = info.Id,
                    ServiceName = service.ServiceName,
                    Client = service.ClientId,

                    //Enable = app.AutoActive,

                    CreateIP = UserHost,
                };
                services.Add(svc);

                var history = AppHistory.Create(app, "RegisterService", true, $"注册服务[{service.ServiceName}] {service.ClientId}", Environment.MachineName, UserHost);
                history.Client = service.ClientId;
                history.SaveAsync();
            }

            // 作用域
            svc.Scope = AppRule.CheckScope(-1, UserHost, service.ClientId);

            // 地址处理。本地任意地址，更换为IP地址
            var ip = service.IP;
            if (ip.IsNullOrEmpty()) ip = service.ClientId.Substring(null, ":");
            if (ip.IsNullOrEmpty()) ip = UserHost;
            var addrs = service.Address
                ?.Replace("://*", $"://{ip}")
                .Replace("://0.0.0.0", $"://{ip}")
                .Replace("://[::]", $"://{ip}");

            svc.Enable = app.AutoActive;
            svc.PingCount++;
            svc.Tag = service.Tag;
            svc.Version = service.Version;
            svc.Address = addrs;

            svc.Save();

            info.Providers = services.Count;
            info.Save();

            return svc;
        }

        [ApiFilter]
        [HttpPost]
        public AppService UnregisterService([FromBody] PublishServiceInfo service, String token)
        {
            var app = _tokenService.DecodeToken(token, Setting.Current.TokenSecret);
            var info = GetService(service.ServiceName);

            // 所有服务
            var services = AppService.FindAllByService(info.Id);
            var svc = services.FirstOrDefault(e => e.AppId == app.Id && e.Client == service.ClientId);
            if (svc != null)
            {
                //svc.Delete();
                svc.Enable = false;
                svc.Update();

                services.Remove(svc);

                var history = AppHistory.Create(app, "UnregisterService", true, $"服务[{service.ServiceName}]下线 {svc.Client}", Environment.MachineName, UserHost);
                history.Client = svc.Client;
                history.SaveAsync();
            }

            info.Providers = services.Count;
            info.Save();

            return svc;
        }

        [ApiFilter]
        [HttpPost]
        public ServiceModel[] ResolveService([FromBody] ConsumeServiceInfo model, String token)
        {
            var app = _tokenService.DecodeToken(token, Setting.Current.TokenSecret);
            var info = GetService(model.ServiceName);

            // 所有消费
            var consumes = AppConsume.FindAllByService(info.Id);
            var svc = consumes.FirstOrDefault(e => e.AppId == app.Id && e.Client == model.ClientId);
            if (svc == null)
            {
                svc = new AppConsume
                {
                    AppId = app.Id,
                    ServiceId = info.Id,
                    ServiceName = model.ServiceName,
                    Client = model.ClientId,

                    Enable = true,

                    CreateIP = UserHost,
                };
                consumes.Add(svc);

                var history = AppHistory.Create(app, "ResolveService", true, $"消费服务[{model.ServiceName}] {model.ToJson()}", Environment.MachineName, UserHost);
                history.Client = svc.Client;
                history.SaveAsync();
            }

            // 作用域
            svc.Scope = AppRule.CheckScope(-1, UserHost, model.ClientId);
            svc.PingCount++;
            svc.Tag = model.Tag;
            svc.MinVersion = model.MinVersion;

            svc.Save();

            info.Consumers = consumes.Count;
            info.Save();

            // 该服务所有生产
            var services = AppService.FindAllByService(info.Id);
            services = services.Where(e => e.Enable).ToList();

            // 匹配minversion和tag
            services = services.Where(e => e.Match(model.MinVersion, svc.Scope, model.Tag?.Split(","))).ToList();

            return services.Select(e => new ServiceModel
            {
                ServiceName = e.ServiceName,
                DisplayName = info.DisplayName,
                Client = e.Client,
                Version = e.Version,
                Address = e.Address,
                Scope = e.Scope,
                Tag = e.Tag,
                Weight = e.Weight,
                CreateTime = e.CreateTime,
                UpdateTime = e.UpdateTime,
            }).ToArray();
        }
        #endregion

        [ApiFilter]
        public IList<AppService> SearchService(String serviceName, String key)
        {
            var svc = Service.FindByName(serviceName);
            if (svc == null) return null;

            return AppService.Search(-1, svc.Id, true, key, new PageParameter { PageSize = 100 });
        }

        #region 辅助
        private void WriteHistory(App node, String action, Boolean success, String remark) => AppHistory.Create(node, action, success, remark, Environment.MachineName, UserHost);
        #endregion
    }
}