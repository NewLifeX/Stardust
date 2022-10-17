using System.Net.WebSockets;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NewLife;
using NewLife.Caching;
using NewLife.Log;
using NewLife.Remoting;
using NewLife.Web;
using Stardust.Data.Nodes;
using Stardust.Models;
using Stardust.Server.Common;
using Stardust.Server.Models;
using Stardust.Server.Services;

namespace Stardust.Server.Controllers;

[ApiController]
[Route("[controller]")]
public class NodeController : BaseController
{
    private Node _node;
    private readonly ICache _queue;
    private readonly ITracer _tracer;
    private readonly NodeService _nodeService;
    private readonly TokenService _tokenService;
    private readonly Setting _setting;

    public NodeController(NodeService nodeService, TokenService tokenService, Setting setting, ICache queue, ITracer tracer)
    {
        _queue = queue;
        _tracer = tracer;
        _nodeService = nodeService;
        _tokenService = tokenService;
        _setting = setting;
    }

    #region 令牌验证
    protected override Boolean OnAuthorize(String token)
    {
        var (node, ex) = _nodeService.DecodeToken(token, _setting.TokenSecret);
        _node = node;
        if (ex != null) throw ex;

        return node != null;
    }

    protected override void OnWriteError(String action, String message) => WriteHistory(_node, action, false, message, UserHost);
    #endregion

    #region 登录
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
        if (node == null || !_nodeService.Auth(node, inf.Secret, inf, ip))
            node = _nodeService.Register(inf, ip, _setting);

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

    #region 心跳
    [HttpPost(nameof(Ping))]
    public PingResponse Ping(PingInfo inf) => _nodeService.Ping(_node, inf, Token, UserHost, _setting);

    [AllowAnonymous]
    [HttpGet(nameof(Ping))]
    public PingResponse Ping() => new() { Time = 0, ServerTime = DateTime.Now, };
    #endregion

    #region 历史
    /// <summary>上报数据，针对命令</summary>
    /// <param name="id"></param>
    /// <returns></returns>
    [HttpPost(nameof(Report))]
    public async Task<Object> Report(Int32 id)
    {
        var node = _node;
        if (node == null) throw new ApiException(402, "节点未登录");

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
    public Int32 CommandReply(CommandReplyModel model) => _node == null ? throw new ApiException(402, "节点未登录") : _nodeService.CommandReply(model, Token);
    #endregion

    #region 升级
    /// <summary>升级检查</summary>
    /// <param name="channel">更新通道</param>
    /// <returns></returns>
    [HttpGet(nameof(Upgrade))]
    public UpgradeInfo Upgrade(String channel)
    {
        var node = _node;
        if (node == null) throw new ApiException(402, "节点未登录");

        var pv = _nodeService.Upgrade(node, channel, UserHost);
        if (pv == null) return null;

        var url = pv.Source;

        // 为了兼容旧版本客户端，这里必须把路径处理为绝对路径
        if (!url.StartsWithIgnoreCase("http://", "https://"))
        {
            var uri = Request.GetRawUrl().ToString();
            var p = uri.IndexOf('/', "https://".Length);
            if (p > 0) uri = uri[..p];
            url = new Uri(new Uri(uri), url) + "";
        }

        return new UpgradeInfo
        {
            Version = pv.Version,
            Source = url,
            FileHash = pv.FileHash,
            Executor = pv.Executor,
            Force = pv.Force,
            Description = pv.Description,
        };
    }

    [Obsolete]
    [HttpGet("GetFile")]
    public ActionResult GetFile(Int32 id)
    {
        var nv = NodeVersion.FindByID(id);
        if (nv == null || !nv.Enable) throw new Exception("非法参数");

        var updatePath = "../Uploads";
        var fi = updatePath.CombinePath(nv.Source).AsFile();
        return !fi.Exists ? throw new Exception("文件不存在") : (ActionResult)File(fi.OpenRead(), "application/octet-stream", Path.GetFileName(nv.Source));
    }

    [Obsolete]
    //[Route("Node/GetVersion/{name}")]
    [HttpGet("GetVersion/{name}")]
    public ActionResult GetVersion(String name)
    {
        var nv = NodeVersion.FindByVersion(name.TrimEnd(".zip"));
        if (nv == null || !nv.Enable) return NotFound("非法参数");

        var updatePath = _setting.UploadPath;
        var fi = updatePath.CombinePath(nv.Source).AsFile();
        if (!fi.Exists) return NotFound("文件不存在");

        //return File(fi.OpenRead(), "application/octet-stream", Path.GetFileName(nv.Source));
        return PhysicalFile(fi.FullName, "application/octet-stream", name);
    }
    #endregion

    #region 下行通知
    /// <summary>下行通知</summary>
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

                await Response.WriteAsync("closed");
            }
        }
        else
        {
            HttpContext.Response.StatusCode = 400;
        }
    }

    private async Task Handle(WebSocket socket, String token, String ip)
    {
        var (node, error) = _nodeService.DecodeToken(token, _setting.TokenSecret);
        if (node == null) throw new ApiException(401, $"未登录！[ip={ip}]");

        _node = node;
        if (error != null) throw error;

        //XTrace.WriteLine("WebSocket连接 {0}", node);
        WriteHistory(node, "WebSocket连接", true, socket.State + "");

        var olt = _nodeService.GetOnline(node, ip) ?? _nodeService.CreateOnline(node, token, ip);
        if (olt != null)
        {
            olt.WebSocket = true;
            olt.SaveAsync();
        }

        var source = new CancellationTokenSource();
        _ = Task.Run(() => ConsumeMessage(socket, node, ip, source));
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
                    XTrace.WriteLine("WebSocket接收 {0} {1}", node, str);
                    WriteHistory(node, "WebSocket接收", true, str);
                }
            }

            source.Cancel();
            //XTrace.WriteLine("WebSocket断开 {0}", node);
            WriteHistory(node, "WebSocket断开", true, socket.State + "");

            await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "finish", default);
        }
        catch (WebSocketException ex)
        {
            XTrace.WriteLine("WebSocket异常 node={0} ip={1}", node, ip);
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

    private async Task ConsumeMessage(WebSocket socket, Node node, String ip, CancellationTokenSource source)
    {
        var cancellationToken = source.Token;
        var queue = _queue.GetQueue<String>($"nodecmd:{node.Code}");
        try
        {
            while (!cancellationToken.IsCancellationRequested && socket.State == WebSocketState.Open)
            {
                var msg = await queue.TakeOneAsync(30);
                if (msg != null)
                {
                    WriteHistory(node, "WebSocket发送", true, msg, ip);

                    await socket.SendAsync(msg.GetBytes(), WebSocketMessageType.Text, true, cancellationToken);
                }
                else
                {
                    await Task.Delay(1_000, cancellationToken);
                }
            }
        }
        catch (TaskCanceledException) { }
        catch (Exception ex)
        {
            XTrace.WriteLine("WebSocket异常 node={0} ip={1}", node, ip);
            XTrace.WriteException(ex);
            WriteHistory(node, "WebSocket断开", false, ex.ToString(), ip);
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
    [AllowAnonymous]
    [HttpPost(nameof(SendCommand))]
    public Int32 SendCommand(CommandInModel model, String token)
    {
        if (model.Code.IsNullOrEmpty()) throw new ArgumentNullException(nameof(model.Code), "必须指定节点");
        if (model.Command.IsNullOrEmpty()) throw new ArgumentNullException(nameof(model.Command));

        return _nodeService.SendCommand(model, token, _setting);
    }
    #endregion

    #region 辅助
    private void WriteHistory(Node node, String action, Boolean success, String remark, String ip = null)
    {
        var hi = NodeHistory.Create(node ?? _node, action, success, remark, Environment.MachineName, ip ?? UserHost);
        hi.SaveAsync();
    }
    #endregion
}