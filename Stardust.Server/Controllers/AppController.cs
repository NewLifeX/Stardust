using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NewLife;
using NewLife.Data;
using NewLife.Log;
using NewLife.Remoting;
using NewLife.Remoting.Extensions;
using NewLife.Remoting.Models;
using NewLife.Serialization;
using Stardust.Data;
using Stardust.Data.Configs;
using Stardust.Models;
using Stardust.Server.Services;
using XCode;
using TokenService = Stardust.Server.Services.TokenService;
using WebSocket = System.Net.WebSockets.WebSocket;

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
    private readonly AppSessionManager _sessionManager;
    private readonly StarServerSetting _setting;

    public AppController(TokenService tokenService, RegistryService registryService, DeployService deployService, AppQueueService queue, AppSessionManager sessionManager, StarServerSetting setting, IServiceProvider serviceProvider, ITracer tracer) : base(serviceProvider)
    {
        _tokenService = tokenService;
        _registryService = registryService;
        _deployService = deployService;
        _queue = queue;
        _sessionManager = sessionManager;
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

    /// <summary>写日志</summary>
    /// <param name="action"></param>
    /// <param name="success"></param>
    /// <param name="message"></param>
    public override void WriteLog(String action, Boolean success, String message)
    {
        var olt = AppOnline.FindByClient(_clientId);

        var hi = AppHistory.Create(_app, action, success, message, olt?.Version, Environment.MachineName, UserHost);
        hi.Client = _clientId;
        hi.Insert();
    }
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
        var ip = UserHost;
        var olt = AppOnline.FindByClient(_clientId);
        var his = new List<AppHistory>();
        foreach (var model in events)
        {
            //WriteHistory(model.Name, !model.Type.EqualIgnoreCase("error"), model.Time.ToDateTime().ToLocalTime(), model.Remark, null);
            var success = !model.Type.EqualIgnoreCase("error");
            var time = model.Time.ToDateTime().ToLocalTime();
            var hi = AppHistory.Create(_app, model.Name, success, model.Remark, olt?.Version, Environment.MachineName, ip);
            hi.Client = _clientId;
            if (time.Year > 2000) hi.CreateTime = time;
            his.Add(hi);
        }

        his.Insert();

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

            await HandleNotify(socket, _app, _clientId, UserHost, HttpContext.RequestAborted);
        }
        else
        {
            HttpContext.Response.StatusCode = 400;
        }
    }

    private async Task HandleNotify(WebSocket socket, App app, String clientId, String ip, CancellationToken cancellationToken)
    {
        if (app == null) throw new ApiException(401, "未登录！");

        using var session = new AppCommandSession(socket)
        {
            Code = $"{app.Name}@{clientId}",
            Log = this,
            SetOnline = online => SetOnline(clientId, online)
        };
        _sessionManager.Add(session);

        await session.WaitAsync(HttpContext, cancellationToken).ConfigureAwait(false);
    }

    private void SetOnline(String clientId, Boolean online)
    {
        var olt = AppOnline.FindByClient(clientId);
        if (olt != null)
        {
            olt.WebSocket = online;
            olt.Update();
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

        var code = model.Code;
        var clientId = "";
        var p = code.IndexOf('@');
        if (p > 0)
        {
            clientId = code[(p + 1)..];
            code = code[..p];
        }

        var target = App.FindByName(code) ?? throw new ArgumentOutOfRangeException(nameof(model.Code), "无效应用");

        var app = _app;
        if (app == null || app.AllowControlNodes.IsNullOrEmpty()) throw new ApiException(401, "无权操作！");

        if (app.AllowControlNodes != "*" && !target.Name.EqualIgnoreCase(app.AllowControlNodes.Split(",")))
            throw new ApiException(403, $"[{app}]无权操作应用[{target}]！\n安全设计需要，默认禁止所有应用向其它应用发送控制指令。\n可在注册中心应用系统中修改[{app}]的可控节点，添加[{target.Name}]，或者设置为*所有应用。");

        var cmd = await _registryService.SendCommand(target, clientId, model, app + "");

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
            await _registryService.NotifyConsumers(svc, "registry/register", app + "");
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
            await _registryService.NotifyConsumers(svc, "registry/unregister", app + "");
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

            _clientId = svc.Client;
            WriteLog("ResolveService", true, $"消费服务[{model.ServiceName}] {model.ToJson()}");
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