using System;
using System.ComponentModel;
using NewLife.Configuration;

namespace StarGateway
{
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
        #endregion
    }
}