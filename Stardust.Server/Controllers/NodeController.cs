using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.Options;
using NewLife;
using NewLife.Log;
using NewLife.Remoting;
using NewLife.Remoting.Extensions;
using NewLife.Remoting.Models;
using NewLife.Remoting.Services;
using NewLife.Security;
using NewLife.Web;
using Stardust.Data.Nodes;
using Stardust.Models;
using Stardust.Server.Services;
using XCode.Membership;
using WebSocket = System.Net.WebSockets.WebSocket;

namespace Stardust.Server.Controllers;

[ApiController]
[Route("[controller]")]
public class NodeController(NodeService nodeService, ITokenService tokenService, NodeSessionManager sessionManager, IServiceProvider serviceProvider, ITracer tracer) : BaseController(nodeService, tokenService, serviceProvider)
{
    private static DateTime _oldVersion = new(2025, 8, 26);

    #region 令牌验证
    protected override Boolean OnAuthorize(String token, DeviceContext context)
    {
        ManageProvider.UserHost = UserHost;

        Exception error = null;
        try
        {
            if (!base.OnAuthorize(token, context)) return false;
        }
        catch (Exception ex)
        {
            error = ex;
        }

        var node = Node.FindByCode(Jwt?.Subject);
        if (node == null || !node.Enable) error ??= new ApiException(ApiCode.Unauthorized, "无效节点");

        Context.Device = node;

        // 旧版本节点允许匿名访问心跳和更新接口
        var actionContext = context["__ActionContext"] as ActionContext;
        var action = (actionContext.ActionDescriptor as ControllerActionDescriptor)?.ActionName;
        if (error is ApiException aex && aex.Code == ApiCode.Unauthorized &&
            node != null && node.CompileTime < _oldVersion &&
            action.EqualIgnoreCase(nameof(Ping), nameof(Upgrade)))
        {
            error = null;
        }

        if (error != null) throw error;

        return node != null;
    }
    #endregion

    #region 登录注销
    [AllowAnonymous]
    [HttpPost(nameof(Login))]
    public ILoginResponse Login(JsonElement data)
    {
        // 由于客户端的多样性，这里需要手工控制序列化。某些客户端的节点信息跟密钥信息在同一层级。
        var jsonOptions = HttpContext.RequestServices.GetService<IOptions<JsonOptions>>();
        var options = jsonOptions.Value.JsonSerializerOptions;
        var inf = data.Deserialize<LoginInfo>(options);
        if (inf.Node == null || inf.Node.UUID.IsNullOrEmpty() && inf.Node.MachineGuid.IsNullOrEmpty() && inf.Node.Macs.IsNullOrEmpty())
        {
            inf.Node = data.Deserialize<NodeInfo>(options);
        }

        var ip = UserHost;
        var code = inf.Code;
        var node = Node.FindByCode(code, true);
        var oldSecret = node?.Secret;
        Context.Device = node;

        if (node != null && !node.Enable) throw new ApiException(ApiCode.Unauthorized, "禁止登录");

        // 支持自动识别2020年的XCoder版本，兼容性处理
        if (inf.ProductCode.IsNullOrEmpty())
        {
            var installPath = inf.Node?.InstallPath;
            if (!installPath.IsNullOrEmpty())
            {
                if (installPath.Contains("XCoder"))
                    inf.ProductCode = "XCoder";
                else if (installPath.Contains("CrazyCoder"))
                    inf.ProductCode = "CrazyCoder";
            }
        }

        //// 设备不存在或者验证失败，执行注册流程
        //if (node != null && !nodeService.Authorize(node, inf.Secret, inf, ip))
        //{
        //    node = null;
        //}

        //node ??= nodeService.Register(inf, ip);
        //Context.Device = node;

        //if (node == null) throw new ApiException(ApiCode.Unauthorized, "节点鉴权失败");

        //var tokenModel = nodeService.Login(node, inf, ip);

        //var rs = new LoginResponse
        //{
        //    Name = node.Name,
        //    Token = tokenModel.AccessToken,
        //};

        var request = inf;
        var rs = nodeService.Login(Context, request, "Http");
        node = Context.Device as Node ?? throw new ApiException(ApiCode.Unauthorized, "节点鉴权失败");

        //rs.Time = inf.Node.Time;
        rs.ServerTime = DateTime.UtcNow.ToLong();

        // 动态注册的设备不可用时，不要发令牌，只发证书
        if (node.Enable)
        {
            if (request.ClientId.IsNullOrEmpty()) Context.ClientId = request.ClientId = Rand.NextString(8);
            var tm = tokenService.IssueToken(node.Code, request.ClientId);

            rs.Token = tm.AccessToken;
            rs.Expire = tm.ExpireIn;
        }

        // 动态注册，下发节点证书
        if (node.Code != code || node.Secret != oldSecret)
        {
            rs.Code = node.Code;
            rs.Secret = node.Secret;
        }

        return rs;
    }

    /// <summary>注销</summary>
    /// <param name="reason">注销原因</param>
    /// <returns></returns>
    [HttpGet(nameof(Logout))]
    [HttpPost(nameof(Logout))]
    public LoginResponse Logout(String reason)
    {
        nodeService.Logout(Context, reason, "Http");

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
    public IPingResponse Ping(PingInfo inf)
    {
        var rs = nodeService.Ping(Context, inf, new MyPingResponse());

        if (Context.Device is Node node)
        {
            rs.Period = node.Period;

            if (rs is IPingResponse2 rs2)
                rs2.NewServer = !node.NewServer.IsNullOrEmpty() ? node.NewServer : node.Project?.NewServer;

            if (rs is MyPingResponse mrs)
            {
                // 服务端设置节点的同步时间周期时，客户端会覆盖掉；服务端未设置时，不要覆盖客户端的同步参数
                if (node.SyncTime > 0) mrs.SyncTime = node.SyncTime;
            }

            // 令牌有效期检查，10分钟内到期的令牌，颁发新令牌，以获取业务的连续性。
            var (jwt, ex) = tokenService.DecodeToken(Token);
            if (ex == null && jwt != null && jwt.Expire < DateTime.Now.AddMinutes(10))
            {
                using var span = tracer?.NewSpan("RefreshNodeToken", new { node.Code, node.Name });

                var tm = tokenService.IssueToken(node.Code, Context.ClientId);
                rs.Token = tm.AccessToken;
            }

            //if (!node.Version.IsNullOrEmpty() && Version.TryParse(node.Version, out var ver))
            //{
            //    // 拉取命令
            //    if (ver.Build >= 2023 && ver.Revision >= 107)
            //        rs.Commands = nodeService.AcquireCommands(node.ID);
            //}
        }

        return rs;
    }

    //[AllowAnonymous]
    //[HttpGet(nameof(Ping))]
    //public PingResponse Ping() => new() { Time = 0, ServerTime = DateTime.UtcNow.ToLong(), };
    #endregion

    #region 升级更新
    /// <summary>升级检查</summary>
    /// <param name="channel">更新通道</param>
    /// <returns></returns>
    [HttpGet(nameof(Upgrade))]
    public IUpgradeInfo Upgrade(String channel)
    {
        var node = Context.Device as Node;

        // 基础路径
        var uri = Request.GetRawUrl().ToString();
        var p = uri.IndexOf('/', "https://".Length);
        if (p > 0) uri = uri[..p];

        var info = nodeService.Upgrade(Context, channel);
        if (info == null)
        {
            nodeService.CheckDotNet(node, new Uri(uri), UserHost);

            return null;
        }

        // 为了兼容旧版本客户端，这里必须把路径处理为绝对路径
        if (info != null && !info.Source.StartsWithIgnoreCase("http://", "https://"))
        {
            info.Source = new Uri(new Uri(uri), info.Source) + "";
        }

        return info!;
    }
    #endregion

    #region 事件上报
    /// <summary>批量上报事件</summary>
    /// <param name="events">事件集合</param>
    /// <returns></returns>
    [ApiFilter]
    [HttpPost(nameof(PostEvents))]
    public Int32 PostEvents(EventModel[] events) => nodeService.PostEvents(Context, events);
    #endregion

    #region 下行通知
    /// <summary>下行通知。通知节点更新、安装和启停应用等</summary>
    /// <returns></returns>
    [HttpGet("/node/notify")]
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
        var node = Context.Device as Node;

        using var span = tracer?.NewSpan("cmd:Ws:Create", node.Code);
        using var session = new NodeCommandSession(socket)
        {
            Code = node.Code,
            Log = this,
            SetOnline = online => nodeService.SetOnline(Context, online),
            Tracer = tracer,
        };
        sessionManager.Add(session);

        await session.WaitAsync(HttpContext, span, cancellationToken);
    }

    /// <summary>向节点发送命令。通知节点更新、安装和启停应用等</summary>
    /// <param name="model"></param>
    /// <param name="token">应用令牌</param>
    /// <returns></returns>
    [AllowAnonymous]
    [HttpPost(nameof(SendCommand))]
    public Task<CommandReplyModel> SendCommand(CommandInModel model)
    {
        if (model.Code.IsNullOrEmpty()) throw new ArgumentNullException(nameof(model.Code), "必须指定节点");
        if (model.Command.IsNullOrEmpty()) throw new ArgumentNullException(nameof(model.Command));

        return nodeService.SendCommand(Context, model);
    }

    /// <summary>设备端响应服务调用</summary>
    /// <param name="model">服务</param>
    /// <returns></returns>
    [HttpPost(nameof(CommandReply))]
    public Int32 CommandReply(CommandReplyModel model) => nodeService.CommandReply(Context, model);
    #endregion
}