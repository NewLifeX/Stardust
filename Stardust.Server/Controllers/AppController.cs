using System.Net;
using System.Net.WebSockets;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NewLife;
using NewLife.Data;
using NewLife.Http;
using NewLife.Log;
using NewLife.Remoting;
using NewLife.Remoting.Models;
using NewLife.Security;
using NewLife.Serialization;
using Stardust.Data;
using Stardust.Data.Configs;
using Stardust.Models;
using Stardust.Server.Services;
using WebSocket = System.Net.WebSockets.WebSocket;
using WebSocketMessageType = System.Net.WebSockets.WebSocketMessageType;

namespace Stardust.Server.Controllers;

/// <summary>应用接口控制器</summary>
[ApiController]
[Route("[controller]")]
public class AppController : BaseController
{
    private App _app;
    private String _clientId;
    private readonly TokenService _tokenService;
    private readonly RegistryService _registryService;
    private readonly DeployService _deployService;
    private readonly ITracer _tracer;
    private readonly AppQueueService _queue;
    private readonly StarServerSetting _setting;
    private readonly IHostApplicationLifetime _lifetime;

    public AppController(TokenService tokenService, RegistryService registryService, DeployService deployService, AppQueueService queue, StarServerSetting setting, IHostApplicationLifetime lifetime, ITracer tracer)
    {
        _tokenService = tokenService;
        _registryService = registryService;
        _deployService = deployService;
        _queue = queue;
        _setting = setting;
        _lifetime = lifetime;
        _tracer = tracer;
    }

    #region 令牌验证
    protected override Boolean OnAuthorize(String token)
    {
        var (jwt, app) = _tokenService.DecodeToken(token, _setting.TokenSecret);
        _app = app;
        _clientId = jwt.Id;

        return app != null;
    }

    protected override void OnWriteError(String action, String message) => WriteHistory(action, false, message, _clientId, UserHost);
    #endregion

    #region 登录&心跳
    [AllowAnonymous]
    [HttpPost(nameof(Login))]
    public LoginResponse Login(AppModel model)
    {
        var set = _setting;
        var ip = UserHost;
        var app = App.FindByName(model.AppId);
        var oldSecret = app?.Secret;
        _app = app;

        // 设备不存在或者验证失败，执行注册流程
        if (app != null && !_registryService.Auth(app, model.Secret, ip, model.ClientId))
        {
            app = null;
        }

        var clientId = model.ClientId;
        app ??= _registryService.Register(model.AppId, model.Secret, set.AppAutoRegister, ip, clientId);
        _app = app ?? throw new ApiException(12, "应用鉴权失败");

        _registryService.Login(app, model, ip, _setting);

        var tokenModel = _tokenService.IssueToken(app.Name, set.TokenSecret, set.TokenExpire, clientId);

        var online = _registryService.SetOnline(_app, model, ip, clientId, Token);

        _deployService.UpdateDeployNode(online);

        var rs = new LoginResponse
        {
            Name = app.DisplayName,
            Token = tokenModel.AccessToken,
        };

        // 动态注册，下发节点证书
        if (app.Name != model.AppId || app.Secret != oldSecret)
        {
            rs.Code = app.Name;
            rs.Secret = app.Secret;
        }

        return rs;
    }

    [HttpPost(nameof(Register))]
    public String Register(AppModel inf)
    {
        var online = _registryService.SetOnline(_app, inf, UserHost, inf.ClientId, Token);

        _deployService.UpdateDeployNode(online);

        return _app?.ToString();
    }

    [HttpPost(nameof(Ping))]
    public PingResponse Ping(AppInfo inf)
    {
        var app = _app;
        var rs = new PingResponse
        {
            Time = inf.Time,
            ServerTime = DateTime.UtcNow.ToLong(),
        };

        var ip = UserHost;
        var online = _registryService.Ping(app, inf, ip, _clientId, Token);
        AppMeter.WriteData(app, inf, "Ping", _clientId, ip);
        _deployService.UpdateDeployNode(online);

        if (app != null)
        {
            rs.Period = app.Period;

            // 令牌有效期检查，10分钟内到期的令牌，颁发新令牌，以获取业务的连续性。
            //todo 这里将来由客户端提交刷新令牌，才能颁发新的访问令牌。
            var set = _setting;
            var tm = _tokenService.ValidAndIssueToken(app.Name, Token, set.TokenSecret, set.TokenExpire, _clientId);
            if (tm != null)
            {
                using var span = _tracer?.NewSpan("RefreshAppToken", new { app.Name, app.DisplayName });

                rs.Token = tm.AccessToken;

                //app.WriteHistory("刷新令牌", true, tm.ToJson(), ip);
            }

            if (!app.Version.IsNullOrEmpty() && Version.TryParse(app.Version, out var ver))
            {
                // 拉取命令
                if (ver.Build >= 2024 && ver.Revision >= 801)
                    rs.Commands = _registryService.AcquireAppCommands(app.Id);
            }
        }

        return rs;
    }

    [AllowAnonymous]
    [HttpGet(nameof(Ping))]
    public PingResponse Ping() => new() { Time = 0, ServerTime = DateTime.UtcNow.ToLong(), };
    #endregion

    #region 事件上报
    /// <summary>批量上报事件</summary>
    /// <param name="events">事件集合</param>
    /// <returns></returns>
    [HttpPost(nameof(PostEvents))]
    public Int32 PostEvents(EventModel[] events)
    {
        foreach (var model in events)
        {
            WriteHistory(model.Name, !model.Type.EqualIgnoreCase("error"), model.Time.ToDateTime().ToLocalTime(), model.Remark, null);
        }

        return events.Length;
    }
    #endregion

    #region 下行通知
    /// <summary>下行通知。通知应用刷新配置信息和服务信息等</summary>
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
        if (app == null) throw new ApiException(401, "未登录！");

        var sid = Rand.Next();
        var connection = HttpContext.Connection;
        var address = connection.RemoteIpAddress ?? IPAddress.Loopback;
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        var remote = new IPEndPoint(address, connection.RemotePort);
        WriteHistory("WebSocket连接", true, $"State={socket.State} sid={sid} Remote={remote}", clientId);

        var olt = AppOnline.FindByClient(clientId);
        if (olt != null)
        {
            olt.WebSocket = true;
            olt.Update();
        }

        var ip = UserHost;
        //var source = new CancellationTokenSource();
        var source = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.ApplicationStopping);
        _ = Task.Run(() => ConsumeMessage(socket, app, clientId, ip, source));

        await socket.WaitForClose(txt =>
        {
            if (txt == "Ping")
            {
                socket.SendAsync("Pong".GetBytes(), WebSocketMessageType.Text, true, source.Token);

                var olt = AppOnline.FindByClient(clientId);
                if (olt != null)
                {
                    olt.WebSocket = true;
                    olt.Update();
                }
            }
        }, source);

        WriteHistory("WebSocket断开", true, $"State={socket.State} CloseStatus={socket.CloseStatus} sid={sid} Remote={remote}", clientId);
        if (olt != null)
        {
            olt.WebSocket = false;
            olt.Update();
        }
    }

    private async Task ConsumeMessage(WebSocket socket, App app, String clientId, String ip, CancellationTokenSource source)
    {
        DefaultSpan.Current = null;
        var cancellationToken = source.Token;
        var queue = _queue.GetQueue(app.Name, clientId);
        try
        {
            while (!cancellationToken.IsCancellationRequested && socket.State == WebSocketState.Open)
            {
                ISpan span = null;
                var mqMsg = await queue.TakeOneAsync(15, cancellationToken);
                if (mqMsg != null)
                {
                    // 埋点
                    span = _tracer?.NewSpan($"mq:AppCommand", mqMsg);

                    // 解码
                    var dic = JsonParser.Decode(mqMsg);
                    var msg = JsonHelper.Convert<CommandModel>(dic);
                    span.Detach(dic);

                    if (msg == null || msg.Id == 0 || msg.Expire.Year > 2000 && msg.Expire < DateTime.UtcNow)
                    {
                        WriteHistory("WebSocket发送", false, "消息无效或已过期。" + mqMsg, clientId, ip);

                        var log = AppCommand.FindById((Int32)msg.Id);
                        if (log != null)
                        {
                            if (log.TraceId.IsNullOrEmpty()) log.TraceId = span?.TraceId;
                            log.Status = CommandStatus.取消;
                            log.Update();
                        }
                    }
                    else
                    {
                        WriteHistory("WebSocket发送", true, mqMsg, clientId, ip);

                        // 向客户端传递埋点信息，构建完整调用链
                        msg.TraceId = span + "";

                        var log = AppCommand.FindById((Int32)msg.Id);
                        if (log != null)
                        {
                            if (log.TraceId.IsNullOrEmpty()) log.TraceId = span?.TraceId;
                            log.Times++;
                            log.Status = CommandStatus.处理中;
                            log.UpdateTime = DateTime.Now;
                            log.Update();
                        }

                        await socket.SendAsync(mqMsg.GetBytes(), WebSocketMessageType.Text, true, cancellationToken);
                    }

                    span?.Dispose();
                }
                else
                {
                    await Task.Delay(100, cancellationToken);
                }
            }
        }
        catch (TaskCanceledException) { }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            XTrace.WriteLine("WebSocket异常 app={0} ip={1}", app, ip);
            XTrace.WriteException(ex);
            WriteHistory("WebSocket断开", false, $"State={socket.State} CloseStatus={socket.CloseStatus} {ex}", clientId, ip);
        }
        finally
        {
            source.Cancel();
        }
    }

    /// <summary>向节点发送命令。通知应用刷新配置信息和服务信息等</summary>
    /// <param name="model"></param>
    /// <param name="token">应用令牌</param>
    /// <returns></returns>
    [HttpPost(nameof(SendCommand))]
    public async Task<Int32> SendCommand(CommandInModel model)
    {
        if (model.Code.IsNullOrEmpty()) throw new ArgumentNullException(nameof(model.Code), "必须指定应用");
        if (model.Command.IsNullOrEmpty()) throw new ArgumentNullException(nameof(model.Command));

        var target = App.FindByName(model.Code);
        if (target == null) throw new ArgumentOutOfRangeException(nameof(model.Code), "无效应用");

        var app = _app;
        if (app == null || app.AllowControlNodes.IsNullOrEmpty()) throw new ApiException(401, "无权操作！");

        if (app.AllowControlNodes != "*" && !target.Name.EqualIgnoreCase(app.AllowControlNodes.Split(",")))
            throw new ApiException(403, $"[{app}]无权操作应用[{target}]！\n安全设计需要，默认禁止所有应用向其它应用发送控制指令。\n可在注册中心应用系统中修改[{app}]的可控节点，添加[{target.Name}]，或者设置为*所有应用。");

        var cmd = await _registryService.SendCommand(target, model, app + "");

        return cmd.Id;
    }

    /// <summary>设备端响应服务调用</summary>
    /// <param name="model">服务</param>
    /// <returns></returns>
    [HttpPost(nameof(CommandReply))]
    public Int32 CommandReply(CommandReplyModel model)
    {
        if (_app == null) throw new ApiException(401, "节点未登录");

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
        if (!info.Enable) throw new ApiException(403, $"服务[{serviceName}]已停用！");

        return info;
    }

    [HttpPost(nameof(RegisterService))]
    public async Task<ServiceModel> RegisterService([FromBody] PublishServiceInfo model)
    {
        var app = _app;
        var info = GetService(model.ServiceName);

        var (svc, changed) = _registryService.RegisterService(app, info, model, UserHost);

        // 发布消息通知消费者
        if (changed)
        {
            await _registryService.NotifyConsumers(info, "registry/register", app + "");
        }

        return svc?.ToModel();
    }

    [HttpPost(nameof(UnregisterService))]
    public async Task<ServiceModel> UnregisterService([FromBody] PublishServiceInfo model)
    {
        var app = _app;
        var info = GetService(model.ServiceName);

        var (svc, changed) = _registryService.UnregisterService(app, info, model, UserHost);

        // 发布消息通知消费者
        if (changed)
        {
            await _registryService.NotifyConsumers(info, "registry/unregister", app + "");
        }

        return svc?.ToModel();
    }

    [HttpPost(nameof(ResolveService))]
    public ServiceModel[] ResolveService([FromBody] ConsumeServiceInfo model)
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

            WriteHistory("ResolveService", true, $"消费服务[{model.ServiceName}] {model.ToJson()}", svc.Client);
        }

        // 节点信息
        var olt = AppOnline.FindByClient(model.ClientId);
        if (olt != null) svc.NodeId = olt.NodeId;

        // 作用域
        svc.Scope = AppRule.CheckScope(-1, UserHost, model.ClientId);
        svc.PingCount++;
        svc.Tag = model.Tag;
        svc.MinVersion = model.MinVersion;

        svc.Save();

        info.Consumers = consumes.Count;
        info.Save();

        var models = _registryService.ResolveService(info, model, svc.Scope);

        // 记录应用消费服务得到的地址
        svc.Address = models?.Select(e => new { e.Address }).ToArray().ToJson();

        svc.Save();

        return models;
    }

    [HttpPost(nameof(SearchService))]
    public IList<AppService> SearchService(String serviceName, String key)
    {
        var svc = Service.FindByName(serviceName);
        if (svc == null) return null;

        return AppService.Search(-1, svc.Id, null, true, key, new PageParameter { PageSize = 100 });
    }
    #endregion

    #region 辅助
    private void WriteHistory(String action, Boolean success, String remark, String clientId, String ip = null)
    {
        var olt = AppOnline.FindByClient(clientId);

        var hi = AppHistory.Create(_app, action, success, remark, olt?.Version, Environment.MachineName, ip ?? UserHost);
        hi.Client = clientId ?? _clientId;
        hi.Insert();
    }

    private void WriteHistory(String action, Boolean success, DateTime time, String remark, String clientId, String ip = null)
    {
        var olt = AppOnline.FindByClient(clientId);

        var hi = AppHistory.Create(_app, action, success, remark, olt?.Version, Environment.MachineName, ip ?? UserHost);
        hi.Client = clientId ?? _clientId;
        if (time.Year > 2000) hi.CreateTime = time;
        hi.Insert();
    }
    #endregion
}