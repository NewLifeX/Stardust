using System;
using System.ComponentModel;
using NewLife;
using NewLife.Configuration;
using NewLife.Security;

namespace Stardust.Server
{
    /// <summary>配置</summary>
    [Config("Stardust")]
    public class Setting : Config<Setting>
    {
        #region 属性
        /// <summary>调试开关。默认true</summary>
        [Description("调试开关。默认true")]
        public Boolean Debug { get; set; } = true;

        /// <summary>服务端口。默认6600</summary>
        [Description("服务端口。默认6600")]
        public Int32 Port { get; set; } = 6600;

        /// <summary>令牌密钥。用于生成JWT令牌的算法和密钥，如HS256:ABCD1234</summary>
        [Description("令牌密钥。用于生成JWT令牌的算法和密钥，如HS256:ABCD1234")]
        public String TokenSecret { get; set; }

        /// <summary>令牌有效期。默认2*3600秒</summary>
        [Description("令牌有效期。默认2*3600秒")]
        public Int32 TokenExpire { get; set; } = 2 * 3600;

        /// <summary>会话超时。默认600秒</summary>
        [Description("会话超时。默认600秒")]
        public Int32 SessionTimeout { get; set; } = 600;

        /// <summary>自动注册。允许客户端自动注册，默认true</summary>
        [Description("自动注册。允许客户端自动注册，默认true")]
        public Boolean AutoRegister { get; set; } = true;

        ///// <summary>APM监控服务器。</summary>
        //[Description("APM监控服务器。")]
        //public String TracerServer { get; set; }
        #endregion

        #region 方法
        /// <summary>加载时触发</summary>
        protected override void OnLoaded()
        {
            if (TokenSecret.IsNullOrEmpty() || TokenSecret.Split(':').Length != 2) TokenSecret = $"HS256:{Rand.NextString(16)}";
            
            base.OnLoaded();
        }
        #endregion
    }
}
