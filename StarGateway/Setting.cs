using System;
using System.ComponentModel;
using NewLife.Configuration;

namespace StarGateway;

/// <summary>配置</summary>
[Config("StarGateway")]
public class StarGatewaySetting : Config<StarGatewaySetting>
{
    #region 属性
    /// <summary>调试开关。默认true</summary>
    [Description("Debug switch. Default true.")]
    public Boolean Debug { get; set; } = true;

    /// <summary>服务端口。默认8800</summary>
    [Description("Service port. Default 8800.")]
    public Int32 Port { get; set; } = 8800;

    /// <summary>令牌密钥。用于生成JWT令牌的算法和密钥，如HS256:ABCD1234</summary>
    [Description("Token secret. Algorithm and secret used to generate JWT tokens, e.g. HS256:ABCD1234")]
    public String TokenSecret { get; set; }

    /// <summary>星尘服务端地址。如 http://star.newlifex.com:6600</summary>
    [Description("Stardust server address, e.g. http://star.newlifex.com:6600")]
    public String StarServer { get; set; }

    /// <summary>本地配置文件路径。StarServer不可达时的兜底配置</summary>
    [Description("Local configuration file path. Fallback configuration used when StarServer is unreachable.")]
    public String LocalConfigFile { get; set; } = "gateway.json";

    /// <summary>Admin API 令牌。非回环地址访问 /api/* 时需携带匹配令牌（请求头 X-Gateway-Token 或 Authorization: Bearer）。留空则仅允许本机回环访问</summary>
    [Description("Admin API token. Non-loopback access to /api/* requires a matching token; leave empty to allow only local loopback")]
    public String AdminToken { get; set; }

    /// <summary>健康检查间隔。单位秒，默认10秒</summary>
    [Description("Health check interval. In seconds, default 10 seconds")]
    public Int32 HealthCheckInterval { get; set; } = 10;

    /// <summary>配置刷新间隔。单位秒，默认15秒</summary>
    [Description("Configuration refresh interval. Unit: seconds, default 15 seconds.")]
    public Int32 ConfigRefreshInterval { get; set; } = 15;

    /// <summary>空闲超时。单位秒，后端超过该时间无流量的连接将被回收，默认900秒（15分钟）</summary>
    [Description("Idle timeout. In seconds; backend connections with no traffic longer than this are reclaimed. Default 900 seconds (15 minutes)")]
    public Int32 IdleTimeout { get; set; } = 900;
    #endregion
}