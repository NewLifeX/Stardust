using NewLife.Configuration;
using System;
using System.ComponentModel;

namespace StarAgent
{
    /// <summary>配置</summary>
    [Config("Star")]
    public class Setting : Config<Setting>
    {
        #region 属性
        /// <summary>调试开关。默认true</summary>
        [Description("调试开关。默认true")]
        public Boolean Debug { get; set; } = true;

        /// <summary>服务地址端口。默认为空，子网内自动发现</summary>
        [Description("服务地址端口。默认为空，子网内自动发现")]
        public String Server { get; set; } = "";

        /// <summary>更新通道。默认Release</summary>
        [Description("更新通道。默认Release")]
        public String Channel { get; set; } = "Release";
        #endregion

        #region 构造
        ///// <summary>实例化</summary>
        //public Setting()
        //{
        //}
        #endregion
    }
}