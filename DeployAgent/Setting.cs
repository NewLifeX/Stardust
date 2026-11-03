using System.ComponentModel;
using NewLife;
using NewLife.Configuration;
using NewLife.Remoting.Clients;
using Stardust;

namespace DeployAgent;

/// <summary>部署代理配置。节点凭据独立存储于 config/DeployAgent.config，与 Star.config 的应用凭据隔离，
/// 避免节点登录写回证书/密钥时污染应用登录</summary>
[Config("DeployAgent")]
public class DeployAgentSetting : Config<DeployAgentSetting>, IClientSetting
{
    #region 属性
    /// <summary>调试开关。默认true</summary>
    [Description("Debug switch. Default true")]
    public Boolean Debug { get; set; } = true;

    /// <summary>星尘服务端地址。如http://star.newlifex.com:6600，默认为空</summary>
    [Description("Stardust server address. For example http://star.newlifex.com:6600; empty by default")]
    public String Server { get; set; } = "";

    /// <summary>节点证书。服务端关闭自动注册时，需与控制台预建节点的编码一致</summary>
    [Description("Node code")]
    public String Code { get; set; } = null!;

    /// <summary>节点密钥。与服务端节点表的密钥配对，同时用于解密下发的仓库密码</summary>
    [Description("Node secret")]
    public String? Secret { get; set; }

    /// <summary>禁用 SSH 严格主机密钥校验。默认false（启用校验）</summary>
    /// <remarks>设为true时跳过主机密钥校验，存在MITM风险，仅在明确知晓风险时使用</remarks>
    [Description("Disable strict SSH host key checking. Default false (checking enabled)")]
    public Boolean DisableSshStrictChecking { get; set; }
    #endregion

    #region 方法
    protected override void OnLoaded()
    {
        // 首次使用时从旧版共用的 Star.config 迁移节点凭据，保证存量部署升级后不掉线
        if (IsNew)
        {
            var star = StarSetting.Current;
            var changed = false;

            if (Server.IsNullOrEmpty() && !star.Server.IsNullOrEmpty())
            {
                Server = star.Server;
                changed = true;
            }
            if (Code.IsNullOrEmpty() && !star.AppKey.IsNullOrEmpty())
            {
                Code = star.AppKey;
                changed = true;
            }
            if (Secret.IsNullOrEmpty() && !star.Secret.IsNullOrEmpty())
            {
                Secret = star.Secret;
                changed = true;
            }
            if (star.DisableSshStrictChecking)
            {
                DisableSshStrictChecking = true;
                changed = true;
            }

            if (changed) Save();
        }

        base.OnLoaded();
    }
    #endregion
}
