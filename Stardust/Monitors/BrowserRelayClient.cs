using System.Net;
using NewLife;
using NewLife.Collections;
using NewLife.Http;
using NewLife.Remoting;

namespace Stardust.Monitors;

/// <summary>浏览器埋点中转客户端</summary>
/// <remarks>
/// 浏览器可以把埋点上报到应用后端，由应用后端转发给星尘，避免前端直接暴露星尘地址或跨域。
/// 应用后端持有 AppId 与 Secret，本客户端自动登录换取应用令牌，并把原始报文转发到
/// <c>Trace/ReportRelay</c>；服务端强制把数据归属到令牌对应的应用，报文里的 AppId 会被忽略，
/// 因此中转方无法向其它应用写入数据。
/// 直报星尘仍是推荐方式：应用后端故障时，前端异常恰恰最需要被回收。
/// </remarks>
/// <example>
/// <code>
/// var relay = new BrowserRelayClient(); // 读取 StarSetting.Current 的 Server/AppKey/Secret
///
/// app.MapPost("/stardust/collect", async (HttpContext ctx) =>
/// {
///     var ip = ctx.Request.Headers["X-Forwarded-For"].ToString().Split(',')[0].Trim();
///     await relay.ForwardAsync(ctx.Request.Body, ip, ctx.RequestAborted);
///     return Results.Ok(new { code = 0 });
/// });
/// </code>
/// </example>
public class BrowserRelayClient : DisposeBase
{
    #region 属性
    /// <summary>星尘服务端地址。如 http://star.newlifex.com:6600</summary>
    public String Server { get; set; }

    /// <summary>应用标识</summary>
    public String AppId { get; set; }

    /// <summary>应用密钥</summary>
    public String Secret { get; set; }

    /// <summary>请求超时时间（毫秒）。默认 10000</summary>
    public Int32 Timeout { get; set; } = 10_000;

    /// <summary>转发报文大小上限（字节）。默认 1048576（1MB）</summary>
    public Int32 MaxBodySize { get; set; } = 1024 * 1024;

    private ApiHttpClient _client;
    private String _clientKey;
    private readonly Object _lock = new();
    #endregion

    #region 构造
    /// <summary>实例化中转客户端</summary>
    public BrowserRelayClient() : this(StarSetting.Current) { }

    /// <summary>实例化中转客户端。从星尘客户端配置读取服务端地址、应用标识与密钥</summary>
    /// <param name="setting">星尘客户端配置</param>
    public BrowserRelayClient(StarSetting setting)
    {
        if (setting == null) throw new ArgumentNullException(nameof(setting));

        Server = setting.Server;
        AppId = setting.AppKey;
        Secret = setting.Secret;
    }

    /// <summary>实例化中转客户端</summary>
    /// <param name="server">星尘服务端地址</param>
    /// <param name="appId">应用标识</param>
    /// <param name="secret">应用密钥</param>
    public BrowserRelayClient(String server, String appId, String secret)
    {
        Server = server;
        AppId = appId;
        Secret = secret;
    }
    #endregion

    #region 方法
    /// <summary>转发浏览器埋点报文到星尘</summary>
    /// <param name="body">浏览器上报的原始 JSON 报文。调用方无需解析，服务端会忽略其中的 AppId</param>
    /// <param name="clientIp">浏览器真实客户端IP。为空或非法时由星尘记录应用后端的来源IP</param>
    /// <param name="cancellationToken">取消通知</param>
    /// <returns>星尘返回的追踪响应</returns>
    public virtual async Task<TraceResponse> ForwardAsync(Stream body, String clientIp, CancellationToken cancellationToken = default)
    {
        if (body == null) throw new ArgumentNullException(nameof(body));

        var client = GetClient();

        var ms = Pool.MemoryStream.Get();
        try
        {
            await body.CopyToAsync(ms, 81920, cancellationToken).ConfigureAwait(false);

            if (ms.Length > MaxBodySize) throw new InvalidOperationException($"The telemetry payload exceeds the {MaxBodySize} bytes limit.");
        }
        catch
        {
            Pool.MemoryStream.Return(ms);
            throw;
        }

        ms.Position = 0;

        var action = "Trace/ReportRelay";
        if (!clientIp.IsNullOrEmpty() && IPAddress.TryParse(clientIp.Trim(), out _))
            action += "?clientIp=" + Uri.EscapeDataString(clientIp.Trim());

        return await client.InvokeAsync<TraceResponse>(HttpMethod.Post, action, ms.Return(true), null, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>获取或创建 API 客户端。地址、应用标识或密钥变化时自动重建</summary>
    /// <returns>API 客户端</returns>
    protected virtual ApiHttpClient GetClient()
    {
        var key = $"{Server}|{AppId}|{Secret}|{Timeout}";
        if (_client != null && _clientKey == key) return _client;

        lock (_lock)
        {
            if (_client != null && _clientKey == key) return _client;

            if (Server.IsNullOrEmpty()) throw new InvalidOperationException("The Stardust server address is not configured.");
            if (AppId.IsNullOrEmpty()) throw new InvalidOperationException("The application identifier is not configured.");

            _client.TryDispose();
            _client = new ApiHttpClient(Server)
            {
                Timeout = Timeout,
                Filter = new TokenHttpFilter
                {
                    UserName = AppId,
                    Password = Secret,
                },
            };
            _clientKey = key;

            return _client;
        }
    }
    #endregion

    #region 销毁
    /// <summary>销毁。释放内部客户端</summary>
    /// <param name="disposing">是否释放托管资源</param>
    protected override void Dispose(Boolean disposing)
    {
        base.Dispose(disposing);

        if (disposing) _client.TryDispose();
    }
    #endregion
}
