using System.Net.WebSockets;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using NewLife;
using NewLife.Caching;
using NewLife.Data;
using NewLife.Log;
using NewLife.Serialization;
using Stardust.Data;
using Stardust.Data.Configs;
using Stardust.Data.Nodes;
using Stardust.Models;
using Stardust.Server.Common;
using Stardust.Server.Models;
using Stardust.Server.Services;

namespace Stardust.Server.Controllers
{
    /// <summary>应用接口控制器</summary>
    [ApiController]
    [Route("[controller]")]
    public class AppController : ControllerBase, IActionFilter
    {
        /// <summary>用户主机</summary>
        public String UserHost => HttpContext.GetUserHost();

        private String _token;
        private App _app;
        private String _clientId;
        private readonly TokenService _tokenService;
        private readonly RegistryService _registryService;
        private readonly ITracer _tracer;
        private static readonly ICache _cache = new MemoryCache();
        private readonly ICache _queue;

        public AppController(TokenService tokenService, RegistryService registryService, ICache queue, ITracer tracer)
        {
            _tokenService = tokenService;
            _registryService = registryService;
            _queue = queue;
            _tracer = tracer;
        }

        #region 令牌验证
        void IActionFilter.OnActionExecuting(ActionExecutingContext context)
        {
            // 从令牌解码应用
            _token = ApiFilterAttribute.GetToken(HttpContext);
            if (!_token.IsNullOrEmpty())
            {
                var (jwt, app) = _tokenService.DecodeToken(_token, Setting.Current.TokenSecret);
                _app = app;
                _clientId = jwt.Id;
            }
        }

        /// <summary>请求处理后</summary>
        /// <param name="context"></param>
        public void OnActionExecuted(ActionExecutedContext context)
        {
            if (context.Exception != null)
            {
                // 拦截全局异常，写日志
                var action = context.HttpContext.Request.Path + "";
                if (context.ActionDescriptor is ControllerActionDescriptor act) action = $"{act.ControllerName}/{act.ActionName}";

                WriteHistory(action, false, context.Exception?.GetTrue() + "", _clientId);
            }
        }
        #endregion

        #region 注册&心跳
        [ApiFilter]
        [HttpPost(nameof(Register))]
        public String Register(AppModel inf)
        {
            _registryService.Register(_app, inf, UserHost, _clientId, _token);

            return _app?.ToString();
        }

        [ApiFilter]
        [HttpPost(nameof(Ping))]
        public PingResponse Ping(AppInfo inf)
        {
            var rs = new PingResponse
            {
                //Time = inf.Time,
                ServerTime = DateTime.UtcNow,
            };

            _registryService.Ping(_app, inf, UserHost, _clientId, _token);

            return rs;
        }

        [ApiFilter]
        [HttpGet(nameof(Ping))]
        public PingResponse Ping() => new() { Time = 0, ServerTime = DateTime.Now, };
        #endregion

        #region 下行通知
        /// <summary>下行通知</summary>
        /// <returns></returns>
        [HttpGet("/app/notify")]
        public async Task Notify()
        {
            if (HttpContext.WebSockets.IsWebSocketRequest)
            {
                using var socket = await HttpContext.WebSockets.AcceptWebSocketAsync();

                await Handle(socket, _app, _clientId);
            }
            else
            {
                HttpContext.Response.StatusCode = 400;
            }
        }

        private async Task Handle(WebSocket socket, App app, String clientId)
        {
            if (app == null) throw new InvalidOperationException("未登录！");

            XTrace.WriteLine("WebSocket连接 {0}", app);
            WriteHistory("WebSocket连接", true, socket.State + "", clientId);

            var source = new CancellationTokenSource();
            _ = Task.Run(() => consumeMessage(socket, app, clientId, UserHost, source));
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
                        WriteHistory("WebSocket接收", true, str, clientId);
                    }
                }

                source.Cancel();
                XTrace.WriteLine("WebSocket断开 {0}", app);
                WriteHistory("WebSocket断开", true, socket.State + "", clientId);

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

        private async Task consumeMessage(WebSocket socket, App app, String clientId, String ip, CancellationTokenSource source)
        {
            DefaultSpan.Current = null;
            var cancellationToken = source.Token;
            var topic = $"appcmd:{app.Name}:{clientId}";
            var queue = _queue.GetQueue<String>(topic);
            try
            {
                while (!cancellationToken.IsCancellationRequested && socket.State == WebSocketState.Open)
                {
                    ISpan span = null;
                    var mqMsg = await queue.TakeOneAsync(30);
                    if (mqMsg != null)
                    {
                        // 埋点
                        span = _tracer?.NewSpan($"mq:AppCommand", mqMsg);

                        // 解码
                        var dic = JsonParser.Decode(mqMsg);
                        var msg = JsonHelper.Convert<CommandModel>(dic);
                        span.Detach(dic);

                        if (msg == null || msg.Id == 0 || msg.Expire.Year > 2000 && msg.Expire < DateTime.Now)
                            WriteHistory("WebSocket发送", false, "消息无效。" + mqMsg, clientId, ip);
                        else
                        {
                            WriteHistory("WebSocket发送", true, mqMsg, clientId, ip);

                            // 向客户端传递埋点信息，构建完整调用链
                            msg.TraceId = span + "";

                            var log = AppCommand.FindById(msg.Id);
                            if (log != null)
                            {
                                if (log.TraceId.IsNullOrEmpty()) log.TraceId = span?.TraceId;
                                log.Status = CommandStatus.处理中;
                                log.Update();
                            }

                            await socket.SendAsync(mqMsg.GetBytes(), WebSocketMessageType.Text, true, cancellationToken);
                        }
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
        public Int32 SendCommand(CommandInModel model)
        {
            if (model.Code.IsNullOrEmpty()) throw new ArgumentNullException(nameof(model.Code), "必须指定应用");
            if (model.Command.IsNullOrEmpty()) throw new ArgumentNullException(nameof(model.Command));

            var node = App.FindByName(model.Code);
            if (node == null) throw new ArgumentOutOfRangeException(nameof(model.Code), "无效应用");

            var app = _app;
            if (app == null || app.AllowControlNodes.IsNullOrEmpty()) throw new InvalidOperationException("无权操作！");

            if (app.AllowControlNodes != "*" && !node.Name.EqualIgnoreCase(app.AllowControlNodes.Split(",")))
                throw new InvalidOperationException($"[{app}]无权操作应用[{node}]！");

            var cmd = _registryService.SendCommand(node, model, app + "");

            return cmd.Id;
        }

        /// <summary>设备端响应服务调用</summary>
        /// <param name="model">服务</param>
        /// <returns></returns>
        [ApiFilter]
        [HttpPost(nameof(CommandReply))]
        public Int32 CommandReply(CommandReplyModel model)
        {
            if (_app == null) throw new NewLife.Remoting.ApiException(402, "节点未登录");

            var cmd = _registryService.CommandReply(_app, model);

            return cmd != null ? 1 : 0;
        }
        #endregion

        #region 服务发布与消费
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
        [HttpPost(nameof(RegisterService))]
        public AppService RegisterService([FromBody] PublishServiceInfo service, String token)
        {
            var app = _app;
            var info = GetService(service.ServiceName);

            // 所有服务
            var services = AppService.FindAllByService(info.Id);
            var isNew = false;
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

                isNew = true;
                WriteHistory("RegisterService", true, $"注册服务[{service.ServiceName}] {service.ClientId}", service.ClientId);
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

            // 发布消息通知消费者
            if (isNew)
            {
                SendCommand(new CommandInModel { Command = "regitry/register" });
            }

            return svc;
        }

        [ApiFilter]
        [HttpPost(nameof(UnregisterService))]
        public AppService UnregisterService([FromBody] PublishServiceInfo service, String token)
        {
            var app = _app;
            var info = GetService(service.ServiceName);

            // 所有服务
            var services = AppService.FindAllByService(info.Id);
            var flag = false;
            var svc = services.FirstOrDefault(e => e.AppId == app.Id && e.Client == service.ClientId);
            if (svc != null)
            {
                //svc.Delete();
                svc.Enable = false;
                svc.Update();

                services.Remove(svc);

                flag = true;
                app.WriteHistory("UnregisterService", true, $"服务[{service.ServiceName}]下线 {svc.Client}", UserHost, svc.Client);
            }

            info.Providers = services.Count;
            info.Save();

            // 发布消息通知消费者
            if (flag)
            {
                SendCommand(new CommandInModel { Command = "regitry/unregister" });
            }

            return svc;
        }

        [ApiFilter]
        [HttpPost(nameof(ResolveService))]
        public ServiceModel[] ResolveService([FromBody] ConsumeServiceInfo model, String token)
        {
            var app = _app;
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

                app.WriteHistory("ResolveService", true, $"消费服务[{model.ServiceName}] {model.ToJson()}", UserHost, svc.Client);
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

        [ApiFilter]
        [HttpPost(nameof(SearchService))]
        public IList<AppService> SearchService(String serviceName, String key)
        {
            var svc = Service.FindByName(serviceName);
            if (svc == null) return null;

            return AppService.Search(-1, svc.Id, true, key, new PageParameter { PageSize = 100 });
        }
        #endregion

        #region 辅助
        private void WriteHistory(String action, Boolean success, String remark, String clientId, String ip = null)
        {
            var hi = AppHistory.Create(_app, action, success, remark, Environment.MachineName, ip ?? UserHost);
            hi.Client = clientId ?? _clientId;
            hi.Insert();
        }
        #endregion
    }
}