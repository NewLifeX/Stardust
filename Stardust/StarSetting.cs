using System.ComponentModel;
using NewLife.Configuration;
using NewLife.Remoting.Clients;

namespace Stardust;

/// <summary>星尘客户端配置</summary>
[DisplayName("Stardust settings")]
[Config("Star")]
public class StarSetting : Config<StarSetting>, IClientSetting
{
    #region 属性
    /// <summary>调试开关。默认false</summary>
    [Description("Debug switch. Default false")]
    public Boolean Debug { get; set; }

    /// <summary>界面语言。如 zh-Hans、zh-Hant、en、ja，留空时跟随系统语言</summary>
    [Description("UI language, for example zh-Hans, zh-Hant, en, or ja; leave empty to follow the system language")]
    public String? Language { get; set; }

    /// <summary>禁用 SSH 严格主机密钥校验。默认false（启用校验）</summary>
    /// <remarks>设为true时跳过主机密钥校验，存在MITM风险，仅在明确知晓风险时使用</remarks>
    [Description("Disable strict SSH host key checking. Default false (checking enabled)")]
    public Boolean DisableSshStrictChecking { get; set; }

    /// <summary>星尘服务端地址。如http://star.newlifex.com:6600，默认为空</summary>
    [Description("Stardust server address. For example http://star.newlifex.com:6600; empty by default")]
    public String Server { get; set; } = "";

    /// <summary>应用标识。接入星尘注册中心，英文名</summary>
    [Description("App key. English name used to register with the Stardust registry")]
    public String? AppKey { get; set; }

    /// <summary>应用密钥。接入星尘注册中心</summary>
    [Description("App secret. Used to register with the Stardust registry")]
    public String? Secret { get; set; }

    /// <summary>服务地址。用户访问的原始外网地址，用于内部构造其它Url，多地址逗号隔开，根据用户访问频率取前5</summary>
    [Description("Service address. Public addresses used to build other URLs; separate multiple addresses with commas and the top 5 by request frequency are used")]
    public String ServiceAddress { get; set; } = "";

    /// <summary>允许的主机。多个主机名逗号隔开，支持*模糊匹配，用于验证请求的Host头，IP地址请求不进行验证。留空则不校验</summary>
    [Description("Allowed hosts. Comma-separated host names with * wildcards, used to validate the Host header; IP requests are not validated. Leave empty to disable validation")]
    public String AllowedHosts { get; set; } = "";

    ///// <summary>用户访问地址。自动记录用户访问的主机地址（反向代理之外），用于内部构造其它Url，多地址逗号隔开</summary>
    //[Description("用户访问地址。自动记录用户访问的主机地址（反向代理之外），用于内部构造其它Url，多地址逗号隔开")]
    //public String UserAddress { get; set; }

    /// <summary>跟踪采样周期。定期上报埋点数据到星尘平台，默认60s</summary>
    [Description("Trace sampling period. Interval for reporting trace data to Stardust, default 60s")]
    public Int32 TracerPeriod { get; set; } = 60;

    /// <summary>最大正常采样数。采样周期内，最多只记录指定数量的正常事件，用于绘制依赖关系，默认1</summary>
    [Description("Maximum normal samples. Maximum normal events recorded per period for dependency graphs, default 1")]
    public Int32 MaxSamples { get; set; } = 1;

    /// <summary>最大异常采样数。采样周期内，最多只记录指定数量的异常事件，默认10</summary>
    [Description("Maximum error samples. Maximum error events recorded per period, default 10")]
    public Int32 MaxErrors { get; set; } = 10;

    /// <summary>采集 OpenTelemetry 埋点。把进程内第三方组件的活动转成埋点上报，默认true</summary>
    [Description("Collect OpenTelemetry traces. Converts third-party component activities into traces, default true")]
    public Boolean EnableOpenTelemetry { get; set; } = true;

    /// <summary>埋点名称截断前缀。自动识别URL路径中的可变参数后提取，多个前缀分号隔开，可手工调整</summary>
    [Description("Trace name prefixes. Variable URL path segments are detected automatically; separate prefixes with semicolons and adjust manually if needed")]
    public String TraceNamePrefixes { get; set; } = "";

    String IClientSetting.Code { get => AppKey!; set => AppKey = value; }
    #endregion
}