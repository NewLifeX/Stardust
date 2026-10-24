using System.ComponentModel;
using NewLife.Configuration;
using NewLife.Remoting.Clients;

namespace StarAgent.Mobile;

/// <summary>移动代理配置</summary>
[Config("StarAgentMobile")]
public class MobileAgentSetting : Config<MobileAgentSetting>, IClientSetting
{
    #region 属性
    /// <summary>服务端地址。支持http/tcp/udp，多地址逗号分隔</summary>
    [Description("Server address, e.g. http://star.newlifex.com:6600")]
    public String Server { get; set; } = "http://127.0.0.1:6600";

    /// <summary>节点编码。留空时由服务端自动分配</summary>
    [Description("Node code. When left empty, it is assigned automatically by the server")]
    public String Code { get; set; } = null!;

    /// <summary>节点密钥</summary>
    [Description("Node secret")]
    public String? Secret { get; set; }

    /// <summary>产品编码。移动端使用独立编码，平台差异由 OSKind 表达</summary>
    [Description("Product code. Default StarAgent-Mobile.")]
    public String ProductCode { get; set; } = "StarAgent-Mobile";

    /// <summary>项目名。新节点默认所要加入的项目</summary>
    [Description("Project name. The project that new nodes join by default")]
    public String? Project { get; set; }

    /// <summary>心跳周期。单位秒，默认60</summary>
    [Description("Heartbeat interval. In seconds; default 60")]
    public Int32 HeartbeatPeriod { get; set; } = 60;

    /// <summary>更新通道。默认Release</summary>
    [Description("Update channel. Default Release.")]
    public String Channel { get; set; } = "Release";

    /// <summary>是否启用文件日志</summary>
    [Description("Whether to enable file logging")]
    public Boolean FileLog { get; set; } = true;
    #endregion
}