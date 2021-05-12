using System;
using System.ComponentModel;
using NewLife;
using NewLife.Common;
using NewLife.Configuration;

namespace Stardust
{
    /// <summary>星尘客户端配置</summary>
    [Config("Star")]
    public class Setting : Config<Setting>
    {
        #region 属性
        /// <summary>调试开关。默认true</summary>
        [Description("调试开关。默认true")]
        public Boolean Debug { get; set; } = true;

        /// <summary>服务端地址。如http://star.newlifex.com:6600，默认为空</summary>
        [Description("服务端地址。如http://star.newlifex.com:6600，默认为空")]
        public String Server { get; set; } = "";

        /// <summary>应用标识</summary>
        [Description("应用标识")]
        public String AppKey { get; set; }

        /// <summary>应用密钥</summary>
        [Description("应用密钥")]
        public String Secret { get; set; }

        /// <summary>本地服务地址。用于提交注册中心，默认为空，自动识别</summary>
        [Description("本地服务地址。用于提交注册中心，默认为空，自动识别")]
        public String ServiceAddress { get; set; }
        #endregion

        #region 方法
        ///// <summary>加载时</summary>
        //protected override void OnLoaded()
        //{
        //    if (AppKey.IsNullOrEmpty()) AppKey = SysConfig.Current.Name;

        //    base.OnLoaded();
        //}
        #endregion
    }
}
