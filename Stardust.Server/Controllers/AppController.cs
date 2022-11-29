using System.Net.WebSockets;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NewLife;
using NewLife.Data;
using NewLife.Log;
using NewLife.Remoting;
using NewLife.Serialization;
using Stardust.Data;
using Stardust.Data.Configs;
using Stardust.Models;
using Stardust.Server.Models;
using Stardust.Server.Services;

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
    private readonly Setting _setting;

    public AppController(TokenService tokenService, RegistryService registryService, DeployService deployService, AppQueueService queue, Setting setting, ITracer tracer)
    {
        _tokenService = tokenService;
        _registryService = registryService;
        _deployService = deployService;
        _queue = queue;
        _setting = setting;
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

    #region 注册&心跳
    [HttpPost(nameof(Register))]
    public String Register(AppModel inf)
    {
        var online = _registryService.Register(_app, inf, UserHost, _clientId, Token);

        _deployService.UpdateDeployNode(online);

        return _app?.ToString();
    }

    [HttpPost(nameof(Ping))]
    public PingResponse Ping(AppInfo inf)
    {
        var rs = new PingResponse
        {
            //Time = inf.Time,
            ServerTime = DateTime.UtcNow,
            Period = _app.Period,
        };

        var online = _registryService.Ping(_app, inf, UserHost, _clientId, Token);

        _deployService.UpdateDeployNode(online);

        return rs;
    }

    [AllowAnonymous]
    [HttpGet(nameof(Ping))]
    public PingResponse Ping() => new() { Time = 0, ServerTime = DateTime.Now, };
    #endregion

    #region 上报
    /// <summary>批量上报事件</summary>
    /// <param name="events">事件集合</param>
    /// <returns></returns>
    [HttpPost(nameof(PostEvents))]
    public Int32 PostEvents(EventModel[] events)
    {
        foreach (var model in events)
        {
            WriteHistory(model.Name, !model.Type.EqualIgnoreCase("error"), model.Remark, null);
        }

        return events.Length;
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

        XTrace.WriteLine("WebSocket连接 {0}", app);
        WriteHistory("WebSocket连接", true, socket.State + "", clientId);

        var olt = AppOnline.FindByClient(clientId);
        if (olt != null)
        {
            olt.WebSocket = true;
            olt.SaveAsync();
        }

        var ip = UserHost;
        var source = new CancellationTokenSource();
        _ = Task.Run(() => ConsumeMessage(socket, app, clientId, ip, source));
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
            XTrace.WriteLine("WebSocket异常 app={0} ip={1}", app, ip);
            XTrace.WriteLine(ex.Message);
        }
        finally
        {
            source.Cancel();

            if (olt != null)
            {
                olt.WebSocket = false;
                olt.SaveAsync();
            }
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
            XTrace.WriteLine("WebSocket异常 app={0} ip={1}", app, ip);
            XTrace.WriteException(ex);
            WriteHistory("WebSocket断开", false, ex.ToString(), clientId, ip);
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
    [HttpPost(nameof(SendCommand))]
    public Int32 SendCommand(CommandInModel model)
    {
        if (model.Code.IsNullOrEmpty()) throw new ArgumentNullException(nameof(model.Code), "必须指定应用");
        if (model.Command.IsNullOrEmpty()) throw new ArgumentNullException(nameof(model.Command));

        var target = App.FindByName(model.Code);
        if (target == null) throw new ArgumentOutOfRangeException(nameof(model.Code), "无效应用");

        var app = _app;
        if (app == null || app.AllowControlNodes.IsNullOrEmpty()) throw new ApiException(401, "无权操作！");

        if (app.AllowControlNodes != "*" && !target.Name.EqualIgnoreCase(app.AllowControlNodes.Split(",")))
            throw new ApiException(403, $"[{app}]无权操作应用[{target}]！");

        var cmd = _registryService.SendCommand(target, model, app + "");

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
    public ServiceModel RegisterService([FromBody] PublishServiceInfo model)
    {
        var app = _app;
        var info = GetService(model.ServiceName);

        var (svc, changed) = _registryService.RegisterService(app, info, model, UserHost);

        // 发布消息通知消费者
        if (changed)
        {
            _registryService.NotifyConsumers(info, "registry/register", app + "");
        }

        return svc?.ToModel();
    }

    [HttpPost(nameof(UnregisterService))]
    public ServiceModel UnregisterService([FromBody] PublishServiceInfo model)
    {
        var app = _app;
        var info = GetService(model.ServiceName);

        var (svc, changed) = _registryService.UnregisterService(app, info, model, UserHost);

        // 发布消息通知消费者
        if (changed)
        {
            _registryService.NotifyConsumers(info, "registry/unregister", app + "");
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

        return _registryService.ResolveService(info, model, svc.Scope);
    }

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
        var olt = AppOnline.FindByClient(clientId);

        var hi = AppHistory.Create(_app, action, success, remark, olt?.Version, Environment.MachineName, ip ?? UserHost);
        hi.Client = clientId ?? _clientId;
        hi.SaveAsync();
    }
    #endregion
}