using System.ComponentModel;
using NewLife.Configuration;
using NewLife.Remoting.Clients;

namespace Stardust;

/// <summary>星尘客户端配置</summary>
[Config("Star")]
public class StarSetting : Config<StarSetting>, IClientSetting
{
    #region 属性
    /// <summary>调试开关。默认false</summary>
    [Description("调试开关。默认false")]
    public Boolean Debug { get; set; }

    /// <summary>星尘服务端地址。如http://star.newlifex.com:6600，默认为空</summary>
    [Description("星尘服务端地址。如http://star.newlifex.com:6600，默认为空")]
    public String Server { get; set; } = "";

    /// <summary>应用标识。接入星尘注册中心，英文名</summary>
    [Description("应用标识。接入星尘注册中心，英文名")]
    public String? AppKey { get; set; }

    /// <summary>应用密钥。接入星尘注册中心</summary>
    [Description("应用密钥。接入星尘注册中心")]
    public String? Secret { get; set; }

    /// <summary>服务地址。用户访问的原始外网地址，用于内部构造其它Url，多地址逗号隔开，根据用户访问频率取前5</summary>
    [Description("服务地址。用户访问的原始外网地址，用于内部构造其它Url，多地址逗号隔开，根据用户访问频率取前5")]
    public String ServiceAddress { get; set; } = "";

    ///// <summary>用户访问地址。自动记录用户访问的主机地址（反向代理之外），用于内部构造其它Url，多地址逗号隔开</summary>
    //[Description("用户访问地址。自动记录用户访问的主机地址（反向代理之外），用于内部构造其它Url，多地址逗号隔开")]
    //public String UserAddress { get; set; }

    /// <summary>跟踪采样周期。定期上报埋点数据到星尘平台，默认60s</summary>
    [Description("跟踪采样周期。定期上报埋点数据到星尘平台，默认60s")]
    public Int32 TracerPeriod { get; set; } = 60;

    /// <summary>最大正常采样数。采样周期内，最多只记录指定数量的正常事件，用于绘制依赖关系，默认1</summary>
    [Description("最大正常采样数。采样周期内，最多只记录指定数量的正常事件，用于绘制依赖关系，默认1")]
    public Int32 MaxSamples { get; set; } = 1;

    /// <summary>最大异常采样数。采样周期内，最多只记录指定数量的异常事件，默认10</summary>
    [Description("最大异常采样数。采样周期内，最多只记录指定数量的异常事件，默认10")]
    public Int32 MaxErrors { get; set; } = 10;

    String IClientSetting.Code { get => AppKey!; set => AppKey = value; }
    #endregion
}