using System;
using System.ComponentModel;
using NewLife.Configuration;

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

        /// <summary>令牌有效期。默认2*3600秒</summary>
        [Description("令牌有效期。默认2*3600秒")]
        public Int32 TokenExpire { get; set; } = 2 * 3600;

        /// <summary>自动注册。允许客户端自动注册，默认true</summary>
        [Description("自动注册。允许客户端自动注册，默认true")]
        public Boolean AutoRegister { get; set; } = true;
        #endregion

        #region 构造
        /// <summary>实例化</summary>
        public Setting()
        {
        }
        #endregion
    }
}
