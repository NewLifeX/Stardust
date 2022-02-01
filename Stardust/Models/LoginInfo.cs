using System;

namespace Stardust.Models
{
    /// <summary>节点登录信息</summary>
    public class LoginInfo
    {
        #region 属性
        /// <summary>节点编码</summary>
        public String Code { get; set; }

        /// <summary>节点密钥</summary>
        public String Secret { get; set; }

        /// <summary>产品编码</summary>
        public String ProductCode { get; set; }

        /// <summary>节点信息</summary>
        public NodeInfo Node { get; set; }
        #endregion
    }

    /// <summary>节点登录响应</summary>
    public class LoginResponse
    {
        #region 属性
        /// <summary>节点编码</summary>
        public String Code { get; set; }

        /// <summary>节点密钥</summary>
        public String Secret { get; set; }

        /// <summary>名称</summary>
        public String Name { get; set; }

        /// <summary>令牌</summary>
        public String Token { get; set; }
        #endregion
    }
}