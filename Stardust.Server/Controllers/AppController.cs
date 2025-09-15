using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NewLife;
using NewLife.Data;
using NewLife.Log;
using NewLife.Remoting;
using NewLife.Remoting.Extensions;
using NewLife.Remoting.Models;
using NewLife.Remoting.Services;
using NewLife.Security;
using NewLife.Serialization;
using Stardust.Data;
using Stardust.Data.Configs;
using Stardust.Models;
using Stardust.Server.Services;
using WebSocket = System.Net.WebSockets.WebSocket;

namespace Stardust.Server.Controllers;

/// <summary>应用接口控制器</summary>
[ApiController]
[Route("[controller]")]
public class AppController(RegistryService registryService, ITokenService tokenService, DeployService deployService, AppSessionManager sessionManager, IServiceProvider serviceProvider, ITracer tracer) : BaseController(registryService, tokenService, serviceProvider)
{
    #region 登录注销
    [AllowAnonymous]
    [HttpPost(nameof(Login))]
    public ILoginResponse Login(AppModel model)
    {
        var ip = UserHost;
        var app = App.FindByName(model.AppId);
        var oldSecret = app?.Secret;
        Context.Device = app;

        //// 设备不存在或者验证失败，执行注册流程
        //if (app != null && !registryService.Authorize(app, model.Secret, ip, model.ClientId))
        //{
        //    app = null;
        //}

        //var clientId = model.ClientId;
        //app ??= registryService.Register(model.AppId, model.Secret, ip, clientId);
        //Context.Device = app ?? throw new ApiException(ApiCode.Unauthorized, "应用鉴权失败");

        //registryService.Login(app, model, ip);

        //var tokenModel = tokenService.IssueToken(app.Name, clientId);

        //var online = registryService.SetOnline(app, model, ip, clientId, Token);

        //deployService.UpdateDeployNode(online);

        //var rs = new LoginResponse
        //{
        //    Name = app.DisplayName,
        //    Token = tokenModel.AccessToken,
        //};

        var request = model;
        var rs = registryService.Login(Context, request, "Http");
        app = Context.Device as App ?? throw new ApiException(ApiCode.Unauthorized, "应用鉴权失败");

        if (Context.Online is AppOnline online) deployService.UpdateDeployNode(online);

        //rs.Time = inf.Node.Time;
        rs.ServerTime = DateTime.UtcNow.ToLong();

        // 动态注册的设备不可用时，不要发令牌，只发证书
        if (app.Enable)
        {
            if (request.ClientId.IsNullOrEmpty()) Context.ClientId = request.ClientId = Rand.NextString(8);
            var tm = tokenService.IssueToken(app.Name, request.ClientId);

            rs.Token = tm.AccessToken;
            rs.Expire = tm.ExpireIn;
        }

        // 动态注册，下发节点证书
        if (app.Name != model.AppId || app.Secret != oldSecret)
        {
            rs.Code = app.Name;
            rs.Secret = app.Secret;
        }

        return rs;
    }

    /// <summary>应用注册。旧版客户端登录接口，新版已废弃，改用Login</summary>
    /// <param name="inf"></param>
    /// <returns></returns>
    [HttpPost(nameof(Register))]
    public String Register(AppModel inf)
    {
        var request = inf;
        var rs = registryService.Login(Context, request, "Http");
        var app = Context.Device as App ?? throw new ApiException(ApiCode.Unauthorized, "应用鉴权失败");

        if (Context.Online is AppOnline online) deployService.UpdateDeployNode(online);

        return app?.ToString();
    }

    /// <summary>注销</summary>
    /// <param name="reason">注销原因</param>
    /// <returns></returns>
    [HttpGet(nameof(Logout))]
    [HttpPost(nameof(Logout))]
    public LoginResponse Logout(String reason)
    {
        registryService.Logout(Context, reason, "Http");

        return new LoginResponse
        {
            Name = Context.Device?.Name,
            Token = null,
        };
    }
    #endregion

    #region 心跳保活
    [HttpGet(nameof(Ping))]
    [HttpPost(nameof(Ping))]
    public IPingResponse Ping(AppInfo inf)
    {
        var rs = registryService.Ping(Context, inf, null);

        var online = Context.Online as AppOnline;
        deployService.UpdateDeployNode(online);

        if (Context.Device is App app)
        {
            AppMeter.WriteData(app, inf, "Ping", Context.ClientId, Context.ClientId);

            //rs.Period = app.Period;

            // 令牌有效期检查，10分钟内到期的令牌，颁发新令牌，以获取业务的连续性。
            var (jwt, ex) = tokenService.DecodeToken(Token);
            if (ex == null && jwt != null && jwt.Expire < DateTime.Now.AddMinutes(10))
            {
                using var span = tracer?.NewSpan("RefreshAppToken", new { app.Name, app.DisplayName });

                var tm = tokenService.IssueToken(app.Name, Context.ClientId);
                rs.Token = tm.AccessToken;
            }

            //if (!app.Version.IsNullOrEmpty() && Version.TryParse(app.Version, out var ver))
            //{
            //    // 拉取命令
            //    if (ver.Build >= 2024 && ver.Revision >= 801)
            //        rs.Commands = registryService.AcquireCommands(app.Id);
            //}
        }

        return rs;
    }

    //[AllowAnonymous]
    //[HttpGet(nameof(Ping))]
    //public PingResponse Ping() => new() { Time = 0, ServerTime = DateTime.UtcNow.ToLong(), };
    #endregion

    #region 事件上报
    /// <summary>批量上报事件</summary>
    /// <param name="events">事件集合</param>
    /// <returns></returns>
    [HttpPost(nameof(PostEvents))]
    public Int32 PostEvents(EventModel[] events) => registryService.PostEvents(Context, events);
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

            await HandleNotify(socket, HttpContext.RequestAborted);
        }
        else
        {
            HttpContext.Response.StatusCode = 400;
        }
    }

    private async Task HandleNotify(WebSocket socket, CancellationToken cancellationToken)
    {
        var app = Context.Device as App ?? throw new InvalidOperationException("未登录！");

        using var span = tracer?.NewSpan("cmd:Ws:Create", app.Name);
        using var session = new AppCommandSession(socket)
        {
            Code = $"{app.Name}@{Context.ClientId}",
            Log = this,
            SetOnline = online => registryService.SetOnline(Context, online),
            Tracer = tracer,
        };
        sessionManager.Add(session);

        await session.WaitAsync(HttpContext, span, cancellationToken);
    }

    /// <summary>向节点发送命令。通知应用刷新配置信息和服务信息等</summary>
    /// <param name="model"></param>
    /// <param name="token">应用令牌</param>
    /// <returns></returns>
    [AllowAnonymous]
    [HttpPost(nameof(SendCommand))]
    public Task<CommandReplyModel> SendCommand(CommandInModel model)
    {
        if (model.Code.IsNullOrEmpty()) throw new ArgumentNullException(nameof(model.Code), "必须指定应用");
        if (model.Command.IsNullOrEmpty()) throw new ArgumentNullException(nameof(model.Command));

        var code = model.Code;
        var clientId = "";
        var p = code.IndexOf('@');
        if (p > 0)
        {
            clientId = code[(p + 1)..];
            code = code[..p];
        }

        var target = App.FindByName(code) ?? throw new ArgumentOutOfRangeException(nameof(model.Code), "无效应用");

        var app = Context.Device as App;
        if (app == null || app.AllowControlNodes.IsNullOrEmpty()) throw new ApiException(ApiCode.Unauthorized, "无权操作！");

        if (app.AllowControlNodes != "*" && !target.Name.EqualIgnoreCase(app.AllowControlNodes.Split(",")))
            throw new ApiException(ApiCode.Forbidden, $"[{app}]无权操作应用[{target}]！\n安全设计需要，默认禁止所有应用向其它应用发送控制指令。\n可在注册中心应用系统中修改[{app}]的可控节点，添加[{target.Name}]，或者设置为*所有应用。");

        return registryService.SendCommand(target, clientId, model, app + "");
    }

    /// <summary>设备端响应服务调用</summary>
    /// <param name="model">服务</param>
    /// <returns></returns>
    [HttpPost(nameof(CommandReply))]
    public Int32 CommandReply(CommandReplyModel model) => registryService.CommandReply(Context, model);
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
        if (!info.Enable) throw new ApiException(ApiCode.Forbidden, $"服务[{serviceName}]已停用！");

        return info;
    }

    [HttpPost(nameof(RegisterService))]
    public async Task<ServiceModel> RegisterService([FromBody] PublishServiceInfo model)
    {
        var app = Context.Device as App;
        var info = GetService(model.ServiceName);

        var online = Context.Online as AppOnline;
        var (svc, changed) = registryService.RegisterService(app, info, model, online, UserHost);

        // 发布消息通知消费者
        if (changed)
        {
            await registryService.NotifyConsumers(svc, "registry/register", app + "");
        }

        return svc?.ToModel();
    }

    [HttpPost(nameof(UnregisterService))]
    public async Task<ServiceModel> UnregisterService([FromBody] PublishServiceInfo model)
    {
        var app = Context.Device as App;
        var info = GetService(model.ServiceName);

        var (svc, changed) = registryService.UnregisterService(app, info, model, UserHost);

        // 发布消息通知消费者
        if (changed)
        {
            await registryService.NotifyConsumers(svc, "registry/unregister", app + "");
        }

        return svc?.ToModel();
    }

    [HttpPost(nameof(ResolveService))]
    public ServiceModel[] ResolveService([FromBody] ConsumeServiceInfo model)
    {
        var app = Context.Device as App;
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

            WriteLog("ResolveService", true, $"消费服务[{model.ServiceName}] {model.ToJson()}");
        }

        // 节点信息
        //var online = AppOnline.FindByClient(model.ClientId);
        var online = Context.Online as AppOnline;
        if (online != null) svc.NodeId = online.NodeId;

        // 作用域
        svc.Scope = AppRule.CheckScope(-1, UserHost, model.ClientId);
        svc.PingCount++;
        svc.Tag = model.Tag;
        svc.MinVersion = model.MinVersion;

        svc.Save();

        info.Consumers = consumes.Count;
        info.Save();

        var models = registryService.ResolveService(info, model, svc.Scope);

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
}