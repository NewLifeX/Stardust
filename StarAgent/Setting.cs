using System;
using System.ComponentModel;
using NewLife.Xml;

namespace StarAgent
{
    /// <summary>配置</summary>
    [XmlConfigFile("Config/Star.config", 15000)]
    public class Setting : XmlConfig<Setting>
    {
        #region 属性
        /// <summary>调试开关。默认true</summary>
        [Description("调试开关。默认true")]
        public Boolean Debug { get; set; } = true;

        /// <summary>服务地址端口。默认为空，子网内自动发现</summary>
        [Description("服务地址端口。默认为空，子网内自动发现")]
        public String Server { get; set; } = "";
        #endregion

        #region 构造
        /// <summary>实例化</summary>
        public Setting()
        {
        }
        #endregion
    }
}
