using NewLife.Configuration;
using NewLife.Xml;
using Stardust.Models;
using System;
using System.ComponentModel;

namespace StarAgent
{
    /// <summary>配置</summary>
    [XmlConfigFile(@"Config\Star.config", 15_000)]
    public class Setting : XmlConfig<Setting>
    {
        #region 属性
        /// <summary>调试开关。默认true</summary>
        [Description("调试开关。默认true")]
        public Boolean Debug { get; set; } = true;

        /// <summary>证书</summary>
        [Description("证书")]
        public String Code { get; set; }

        /// <summary>密钥</summary>
        [Description("密钥")]
        public String Secret { get; set; }

        /// <summary>服务地址端口。默认为空，子网内自动发现</summary>
        [Description("服务地址端口。默认为空，子网内自动发现")]
        public String Server { get; set; } = "";

        /// <summary>更新通道。默认Release</summary>
        [Description("更新通道。默认Release")]
        public String Channel { get; set; } = "Release";

        /// <summary>应用服务集合</summary>
        [Description("应用服务集合")]
        public ServiceInfo[] Services { get; set; }
        #endregion

        #region 构造
        ///// <summary>实例化</summary>
        //public Setting()
        //{
        //}
        #endregion

        #region 方法
        protected override void OnLoaded()
        {
            if (Services == null || Services.Length == 0)
            {
                var si = new ServiceInfo
                {
                    Name = "test",
                    FileName = "cmd",
                    Arguments = "ping newlifex.com",

                    //AutoStart = false,
                    AutoRestart = true,
                    //RestartExistCodes = "0,1,3",
                };

                Services = new[] { si };
            }

            base.OnLoaded();
        }
        #endregion
    }
}