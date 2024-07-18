using NewLife.Remoting.Models;

namespace Stardust.Models;

/// <summary>节点登录信息</summary>
public class LoginInfo : ILoginRequest
{
    #region 属性
    /// <summary>节点编码</summary>
    public String? Code { get; set; }

    /// <summary>节点密钥</summary>
    public String? Secret { get; set; }

    /// <summary>产品编码</summary>
    public String? ProductCode { get; set; }

    /// <summary>实例。应用可能多实例部署，ip@proccessid</summary>
    public String? ClientId { get; set; }

    /// <summary>项目名。新节点默认所需要加入的项目</summary>
    public String? Project { get; set; }

    /// <summary>节点信息</summary>
    public NodeInfo? Node { get; set; }
    #endregion
}

///// <summary>节点登录响应</summary>
//public class LoginResponse : ILoginResponse
//{
//    #region 属性
//    /// <summary>节点编码</summary>
//    public String? Code { get; set; }

//    /// <summary>节点密钥</summary>
//    public String? Secret { get; set; }

//    /// <summary>名称</summary>
//    public String? Name { get; set; }

//    /// <summary>令牌</summary>
//    public String? Token { get; set; }

//    /// <summary>服务器时间。Unix毫秒（UTC）</summary>
//    public Int64 Time { get; set; }
//    #endregion
//}