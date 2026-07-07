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
    [Description("调试开关。默认true")]
    public Boolean Debug { get; set; } = true;

    /// <summary>服务端口。默认8800</summary>
    [Description("服务端口。默认8800")]
    public Int32 Port { get; set; } = 8800;

    /// <summary>令牌密钥。用于生成JWT令牌的算法和密钥，如HS256:ABCD1234</summary>
    [Description("令牌密钥。用于生成JWT令牌的算法和密钥，如HS256:ABCD1234")]
    public String TokenSecret { get; set; }

    /// <summary>星尘服务端地址。如 http://star.newlifex.com:6600</summary>
    [Description("星尘服务端地址。如 http://star.newlifex.com:6600")]
    public String StarServer { get; set; }

    /// <summary>本地配置文件路径。StarServer不可达时的兜底配置</summary>
    [Description("本地配置文件路径。StarServer不可达时的兜底配置")]
    public String LocalConfigFile { get; set; } = "gateway.json";

    /// <summary>健康检查间隔。单位秒，默认10秒</summary>
    [Description("健康检查间隔。单位秒，默认10秒")]
    public Int32 HealthCheckInterval { get; set; } = 10;

    /// <summary>配置刷新间隔。单位秒，默认15秒</summary>
    [Description("配置刷新间隔。单位秒，默认15秒")]
    public Int32 ConfigRefreshInterval { get; set; } = 15;
    #endregion
}