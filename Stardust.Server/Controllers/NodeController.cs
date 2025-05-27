using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using NewLife;
using NewLife.Caching;
using NewLife.Log;
using NewLife.Remoting;
using NewLife.Remoting.Extensions;
using NewLife.Remoting.Models;
using NewLife.Web;
using Stardust.Data.Deployment;
using Stardust.Data.Nodes;
using Stardust.Models;
using Stardust.Server.Services;
using XCode;
using XCode.Membership;
using TokenService = Stardust.Server.Services.TokenService;
using WebSocket = System.Net.WebSockets.WebSocket;

namespace Stardust.Server.Controllers;

[ApiController]
[Route("[controller]")]
public class NodeController : BaseController
{
    private Node _node;
    private String _clientId;
    private readonly ICacheProvider _cacheProvider;
    private readonly ITracer _tracer;
    private readonly IOptions<JsonOptions> _jsonOptions;
    private readonly NodeService _nodeService;
    private readonly TokenService _tokenService;
    private readonly DeployService _deployService;
    private readonly NodeSessionManager _sessionManager;
    private readonly StarServerSetting _setting;

    public NodeController(NodeService nodeService, TokenService tokenService, DeployService deployService, NodeSessionManager sessionManager, StarServerSetting setting, ICacheProvider cacheProvider, IServiceProvider serviceProvider, ITracer tracer, IOptions<JsonOptions> jsonOptions) : base(serviceProvider)
    {
        _cacheProvider = cacheProvider;
        _tracer = tracer;
        this._jsonOptions = jsonOptions;
        _nodeService = nodeService;
        _tokenService = tokenService;
        _deployService = deployService;
        _sessionManager = sessionManager;
        _setting = setting;
    }

    #region 令牌验证
    protected override Boolean OnAuthorize(String token)
    {
        ManageProvider.UserHost = UserHost;

        var (jwt, node, ex) = _nodeService.DecodeToken(token, _setting.TokenSecret);
        _node = node;
        _clientId = jwt.Id;
        if (ex != null) throw ex;

        return node != null;
    }

    /// <summary>写日志</summary>
    /// <param name="action"></param>
    /// <param name="success"></param>
    /// <param name="message"></param>
    public override void WriteLog(String action, Boolean success, String message)
    {
        var hi = NodeHistory.Create(_node, action, success, message, Environment.MachineName, UserHost);
        hi.Insert();
    }
    #endregion

    #region 登录注销
    [AllowAnonymous]
    [HttpPost(nameof(Login))]
    public LoginResponse Login(JsonElement data)
    {
        // 由于客户端的多样性，这里需要手工控制序列化。某些客户端的节点信息跟密钥信息在同一层级。
        var options = _jsonOptions.Value.JsonSerializerOptions;
        var inf = data.Deserialize<LoginInfo>(options);
        if (inf.Node == null || inf.Node.UUID.IsNullOrEmpty() && inf.Node.MachineGuid.IsNullOrEmpty() && inf.Node.Macs.IsNullOrEmpty())
        {
            inf.Node = data.Deserialize<NodeInfo>(options);
        }

        var ip = UserHost;
        var code = inf.Code;
        var node = Node.FindByCode(code, true);
        var oldSecret = node?.Secret;
        _node = node;

        if (node != null && !node.Enable) throw new ApiException(99, "禁止登录");

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

        // 设备不存在或者验证失败，执行注册流程
        if (node != null && !_nodeService.Auth(node, inf.Secret, inf, ip, _setting))
        {
            node = null;
        }

        node ??= _nodeService.Register(inf, ip, _setting);

        if (node == null) throw new ApiException(12, "节点鉴权失败");

        var tokenModel = _nodeService.Login(node, inf, ip, _setting);

        var rs = new LoginResponse
        {
            Name = node.Name,
            Token = tokenModel.AccessToken,
        };

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
        if (_node != null) _nodeService.Logout(_node, reason, UserHost);

        return new LoginResponse
        {
            Name = _node?.Name,
            Token = null,
        };
    }
    #endregion

    #region 心跳保活
    [HttpPost(nameof(Ping))]
    public PingResponse Ping(PingInfo inf)
    {
        var node = _node;
        var rs = new MyPingResponse
        {
            Time = inf.Time,
            ServerTime = DateTime.UtcNow.ToLong(),
        };

        var online = _nodeService.Ping(node, inf, Token, UserHost);

        if (node != null)
        {
            rs.Period = node.Period;
            rs.NewServer = !node.NewServer.IsNullOrEmpty() ? node.NewServer : node.Project?.NewServer;

            // 服务端设置节点的同步时间周期时，客户端会覆盖掉；服务端未设置时，不要覆盖客户端的同步参数
            if (node.SyncTime > 0) rs.SyncTime = node.SyncTime;

            // 令牌有效期检查，10分钟内到期的令牌，颁发新令牌，以获取业务的连续性。
            //todo 这里将来由客户端提交刷新令牌，才能颁发新的访问令牌。
            var set = _setting;
            var tm = _tokenService.ValidAndIssueToken(node.Code, Token, set.TokenSecret, set.TokenExpire, _clientId);
            if (tm != null)
            {
                using var span = _tracer?.NewSpan("RefreshNodeToken", new { node.Code, node.Name });

                rs.Token = tm.AccessToken;

                //node.WriteHistory("刷新令牌", true, tm.ToJson(), ip);
            }

            if (!node.Version.IsNullOrEmpty() && Version.TryParse(node.Version, out var ver))
            {
                // 拉取命令
                if (ver.Build >= 2023 && ver.Revision >= 107)
                    rs.Commands = _nodeService.AcquireNodeCommands(node.ID);
            }
        }

        return rs;
    }

    [AllowAnonymous]
    [HttpGet(nameof(Ping))]
    public PingResponse Ping() => new() { Time = 0, ServerTime = DateTime.UtcNow.ToLong(), };
    #endregion

    #region 升级更新
    /// <summary>升级检查</summary>
    /// <param name="channel">更新通道</param>
    /// <returns></returns>
    [HttpGet(nameof(Upgrade))]
    public UpgradeInfo Upgrade(String channel)
    {
        var node = _node ?? throw new ApiException(401, "节点未登录");

        // 基础路径
        var uri = Request.GetRawUrl().ToString();
        var p = uri.IndexOf('/', "https://".Length);
        if (p > 0) uri = uri[..p];

        var pv = _nodeService.Upgrade(node, channel, UserHost);
        if (pv == null)
        {
            _nodeService.CheckDotNet(node, new Uri(uri), UserHost);

            return null;
        }

        var url = pv.Source;

        // 为了兼容旧版本客户端，这里必须把路径处理为绝对路径
        if (!url.StartsWithIgnoreCase("http://", "https://"))
        {
            url = new Uri(new Uri(uri), url) + "";
        }

        return new UpgradeInfo
        {
            Version = pv.Version,
            Source = url,
            FileHash = pv.FileHash,
            Preinstall = pv.Preinstall,
            Executor = pv.Executor,
            Force = pv.Force,
            Description = pv.Description,
        };
    }
    #endregion

    #region 事件上报
    /// <summary>批量上报事件</summary>
    /// <param name="events">事件集合</param>
    /// <returns></returns>
    [ApiFilter]
    [HttpPost(nameof(PostEvents))]
    public Int32 PostEvents(EventModel[] events)
    {
        var ip = UserHost;
        var his = new List<NodeHistory>();
        var dis = new List<AppDeployHistory>();
        foreach (var model in events)
        {
            var success = !model.Type.EqualIgnoreCase("error");
            if (model.Name.EqualIgnoreCase("ServiceController"))
            {
                var appId = 0;
                var p = model.Type.LastIndexOf('-');
                if (p > 0)
                {
                    success = !model.Type[(p + 1)..].EqualIgnoreCase("error");
                    appId = AppDeploy.FindByName(model.Type[..p])?.Id ?? 0;
                }

                //_deployService.WriteHistory(appId, _node?.ID ?? 0, model.Name, success, model.Remark, UserHost);
                var dhi = AppDeployHistory.Create(appId, _node?.ID ?? 0, model.Name, success, model.Remark, ip);
                dis.Add(dhi);
            }

            //WriteHistory(null, model.Name, success, model.Time.ToDateTime().ToLocalTime(), model.Remark);
            var hi = NodeHistory.Create(_node, model.Name, success, model.Remark, Environment.MachineName, ip);
            var time = model.Time.ToDateTime().ToLocalTime();
            if (time.Year > 2000) hi.CreateTime = time;
            his.Add(hi);
        }

        his.Insert();
        dis.Insert();

        return events.Length;
    }

    /// <summary>上报数据，针对命令</summary>
    /// <param name="id"></param>
    /// <returns></returns>
    [HttpPost(nameof(Report))]
    public async Task<Object> Report(Int32 id)
    {
        var node = _node ?? throw new ApiException(401, "节点未登录");

        var cmd = NodeCommand.FindById(id);
        if (cmd != null && cmd.NodeID == node.ID)
        {
            var ms = Request.Body;
            if (Request.ContentLength > 0)
            {
                var rs = cmd.Command switch
                {
                    "截屏" => await SaveFileAsync(cmd, ms, "png"),
                    "抓日志" => await SaveFileAsync(cmd, ms, "log"),
                    _ => await SaveFileAsync(cmd, ms, "bin"),
                };
                if (!rs.IsNullOrEmpty())
                {
                    cmd.Status = CommandStatus.已完成;
                    cmd.Result = rs;
                    cmd.Save();

                    WriteLog(cmd.Command, true, rs);
                }
            }
        }

        return null;
    }

    private async Task<String> SaveFileAsync(NodeCommand cmd, Stream ms, String ext)
    {
        var file = $"../{cmd.Command}/{DateTime.Today:yyyyMMdd}/{cmd.NodeID}_{cmd.Id}.{ext}";
        file.EnsureDirectory(true);

        using var fs = file.AsFile().OpenWrite();
        await ms.CopyToAsync(fs);
        await ms.FlushAsync();

        return file;
    }

    /// <summary>设备端响应服务调用</summary>
    /// <param name="model">服务</param>
    /// <returns></returns>
    [HttpPost(nameof(CommandReply))]
    public Int32 CommandReply(CommandReplyModel model) => _node == null ? throw new ApiException(401, "节点未登录") : _nodeService.CommandReply(_node, model, Token);
    #endregion

    #region 下行通知
    /// <summary>下行通知。通知节点更新、安装和启停应用等</summary>
    /// <returns></returns>
    [HttpGet("/node/notify")]
    public async Task Notify()
    {
        if (HttpContext.WebSockets.IsWebSocketRequest)
        {
            var ip = UserHost;
            var token = (HttpContext.Request.Headers["Authorization"] + "").TrimStart("Bearer ");
            using var socket = await HttpContext.WebSockets.AcceptWebSocketAsync();
            try
            {
                await HandleNotify(socket, token, ip, HttpContext.RequestAborted);
            }
            catch (Exception ex)
            {
                XTrace.WriteLine("WebSocket异常 node={0} ip={1}", _node, ip);
                XTrace.WriteException(ex);

                WriteLog("Node/Notify", false, ex?.GetTrue() + "");
            }
        }
        else
        {
            HttpContext.Response.StatusCode = 400;
        }
    }

    private async Task HandleNotify(WebSocket socket, String token, String ip, CancellationToken cancellationToken)
    {
        var (_, node, error) = _nodeService.DecodeToken(token, _setting.TokenSecret);
        _node = node ?? throw new ApiException(401, $"未登录！[ip={ip}]");
        if (error != null) throw error;

        using var session = new NodeCommandSession(socket)
        {
            Code = node.Code,
            Log = this,
            SetOnline = online => SetOnline(node, token, ip, online)
        };
        _sessionManager.Add(session);

        await session.WaitAsync(HttpContext, cancellationToken).ConfigureAwait(false);
    }

    private void SetOnline(Node node, String token, String ip, Boolean online)
    {
        var olt = _nodeService.GetOrAddOnline(node, token, ip);
        if (olt != null)
        {
            olt.WebSocket = online;
            olt.Update();
        }
    }

    /// <summary>向节点发送命令。通知节点更新、安装和启停应用等</summary>
    /// <param name="model"></param>
    /// <param name="token">应用令牌</param>
    /// <returns></returns>
    [AllowAnonymous]
    [HttpPost(nameof(SendCommand))]
    public async Task<Int32> SendCommand(CommandInModel model, String token)
    {
        if (model.Code.IsNullOrEmpty()) throw new ArgumentNullException(nameof(model.Code), "必须指定节点");
        if (model.Command.IsNullOrEmpty()) throw new ArgumentNullException(nameof(model.Command));

        var cmd = await _nodeService.SendCommand(model, token, _setting);

        return cmd.Id;
    }
    #endregion

    #region 辅助
    private void WriteHistory(Node node, String action, Boolean success, DateTime time, String remark, String ip = null)
    {
        var hi = NodeHistory.Create(node ?? _node, action, success, remark, Environment.MachineName, ip ?? UserHost);
        if (time.Year > 2000) hi.CreateTime = time;
        hi.Insert();
    }
    #endregion
}