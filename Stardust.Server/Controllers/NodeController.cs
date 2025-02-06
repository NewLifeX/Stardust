using System.Net;
using System.Net.WebSockets;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NewLife;
using NewLife.Caching;
using NewLife.Http;
using NewLife.Log;
using NewLife.Remoting;
using NewLife.Remoting.Extensions;
using NewLife.Remoting.Models;
using NewLife.Security;
using NewLife.Serialization;
using NewLife.Web;
using Stardust.Data.Deployment;
using Stardust.Data.Nodes;
using Stardust.Models;
using Stardust.Server.Services;
using WebSocket = System.Net.WebSockets.WebSocket;
using WebSocketMessageType = System.Net.WebSockets.WebSocketMessageType;

namespace Stardust.Server.Controllers;

[ApiController]
[Route("[controller]")]
public class NodeController : BaseController
{
    private Node _node;
    private String _clientId;
    private readonly ICacheProvider _cacheProvider;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly ITracer _tracer;
    private readonly NodeService _nodeService;
    private readonly TokenService _tokenService;
    private readonly DeployService _deployService;
    private readonly StarServerSetting _setting;

    public NodeController(NodeService nodeService, TokenService tokenService, DeployService deployService, StarServerSetting setting, ICacheProvider cacheProvider, IHostApplicationLifetime lifetime, ITracer tracer)
    {
        _cacheProvider = cacheProvider;
        _lifetime = lifetime;
        _tracer = tracer;
        _nodeService = nodeService;
        _tokenService = tokenService;
        _deployService = deployService;
        _setting = setting;
    }

    #region 令牌验证
    protected override Boolean OnAuthorize(String token)
    {
        var (jwt, node, ex) = _nodeService.DecodeToken(token, _setting.TokenSecret);
        _node = node;
        _clientId = jwt.Id;
        if (ex != null) throw ex;

        return node != null;
    }

    protected override void OnWriteError(String action, String message) => WriteHistory(_node, action, false, message, UserHost);
    #endregion

    #region 登录注销
    [AllowAnonymous]
    [HttpPost(nameof(Login))]
    public LoginResponse Login(LoginInfo inf)
    {
        var ip = UserHost;
        var code = inf.Code;
        var node = Node.FindByCode(code, true);
        var oldSecret = node?.Secret;
        _node = node;

        if (node != null && !node.Enable) throw new ApiException(99, "禁止登录");

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
        var rs = new PingResponse
        {
            Time = inf.Time,
            ServerTime = DateTime.UtcNow.ToLong(),
        };

        var online = _nodeService.Ping(node, inf, Token, UserHost);

        if (node != null)
        {
            rs.Period = node.Period;
            rs.NewServer = !node.NewServer.IsNullOrEmpty() ? node.NewServer : node.Project?.NewServer;

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

                _deployService.WriteHistory(appId, _node?.ID ?? 0, model.Name, success, model.Remark, UserHost);
            }

            WriteHistory(null, model.Name, success, model.Time.ToDateTime().ToLocalTime(), model.Remark);
        }

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

                    WriteHistory(node, cmd.Command, true, rs);
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
                await Handle(socket, token, ip);
            }
            catch (Exception ex)
            {
                XTrace.WriteLine("WebSocket异常 node={0} ip={1}", _node, UserHost);
                XTrace.WriteException(ex);

                WriteHistory(_node, "Node/Notify", false, ex?.GetTrue() + "");
            }
        }
        else
        {
            HttpContext.Response.StatusCode = 400;
        }
    }

    private async Task Handle(WebSocket socket, String token, String ip)
    {
        var (_, node, error) = _nodeService.DecodeToken(token, _setting.TokenSecret);
        _node = node ?? throw new ApiException(401, $"未登录！[ip={ip}]");
        if (error != null) throw error;

        var sid = Rand.Next();
        var connection = HttpContext.Connection;
        var address = connection.RemoteIpAddress ?? IPAddress.Loopback;
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        var remote = new IPEndPoint(address, connection.RemotePort);
        WriteHistory(node, "WebSocket连接", true, $"State={socket.State} sid={sid} Remote={remote}");

        var olt = _nodeService.GetOrAddOnline(node, token, ip);
        if (olt != null)
        {
            olt.WebSocket = true;
            olt.Update();
        }

        var source = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.ApplicationStopping);
        _ = Task.Run(() => ConsumeMessage(socket, node, ip, source));

        await socket.WaitForClose(txt =>
        {
            if (txt == "Ping")
            {
                socket.SendAsync("Pong".GetBytes(), WebSocketMessageType.Text, true, source.Token);

                var olt = _nodeService.GetOrAddOnline(node, token, ip);
                if (olt != null)
                {
                    olt.WebSocket = true;
                    olt.Update();
                }
            }
        }, source);

        WriteHistory(node, "WebSocket断开", true, $"State={socket.State} CloseStatus={socket.CloseStatus} sid={sid} Remote={remote}");
        if (olt != null)
        {
            olt.WebSocket = false;
            olt.Update();
        }
    }

    private async Task ConsumeMessage(WebSocket socket, Node node, String ip, CancellationTokenSource source)
    {
        DefaultSpan.Current = null;
        var cancellationToken = source.Token;
        var queue = _cacheProvider.GetQueue<String>($"nodecmd:{node.Code}");
        try
        {
            while (!cancellationToken.IsCancellationRequested && socket.State == WebSocketState.Open)
            {
                ISpan span = null;
                var mqMsg = await queue.TakeOneAsync(15, cancellationToken);
                if (mqMsg != null)
                {
                    // 埋点
                    span = _tracer?.NewSpan($"mq:NodeCommand", mqMsg);

                    // 解码
                    var dic = JsonParser.Decode(mqMsg);
                    var msg = JsonHelper.Convert<CommandModel>(dic);
                    span.Detach(dic);

                    if (msg == null || msg.Id == 0 || msg.Expire.Year > 2000 && msg.Expire < DateTime.UtcNow)
                    {
                        WriteHistory(node, "WebSocket发送", false, "消息无效或已过期。" + mqMsg, ip);

                        var log = NodeCommand.FindById((Int32)msg.Id);
                        if (log != null)
                        {
                            if (log.TraceId.IsNullOrEmpty()) log.TraceId = span?.TraceId;
                            log.Status = CommandStatus.取消;
                            log.Update();
                        }
                    }
                    else
                    {
                        WriteHistory(node, "WebSocket发送", true, mqMsg, ip);

                        // 向客户端传递埋点信息，构建完整调用链
                        msg.TraceId = span + "";

                        var log = NodeCommand.FindById((Int32)msg.Id);
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
                    await Task.Delay(1_000, cancellationToken);
                }
            }
        }
        catch (TaskCanceledException) { }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            XTrace.WriteLine("WebSocket异常 node={0} ip={1}", node, ip);
            XTrace.WriteException(ex);
            WriteHistory(node, "WebSocket断开", false, $"State={socket.State} CloseStatus={socket.CloseStatus} {ex}", ip);
        }
        finally
        {
            source.Cancel();
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
    private void WriteHistory(Node node, String action, Boolean success, String remark, String ip = null)
    {
        var hi = NodeHistory.Create(node ?? _node, action, success, remark, Environment.MachineName, ip ?? UserHost);
        hi.Insert();
    }

    private void WriteHistory(Node node, String action, Boolean success, DateTime time, String remark, String ip = null)
    {
        var hi = NodeHistory.Create(node ?? _node, action, success, remark, Environment.MachineName, ip ?? UserHost);
        if (time.Year > 2000) hi.CreateTime = time;
        hi.Insert();
    }
    #endregion
}