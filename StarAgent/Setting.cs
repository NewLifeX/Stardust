﻿using NewLife;
using NewLife.Configuration;
using NewLife.Xml;
using Stardust.Models;
using System;
using System.ComponentModel;
using System.Linq;

namespace StarAgent
{
    /// <summary>配置</summary>
    [Config("StarAgent")]
    public class Setting : Config<Setting>
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

        /// <summary>本地服务。默认udp://127.0.0.1:5500</summary>
        [Description("本地服务。默认udp://127.0.0.1:5500")]
        public String LocalServer { get; set; } = "udp://127.0.0.1:5500";

        /// <summary>更新通道。默认Release</summary>
        [Description("更新通道。默认Release")]
        public String Channel { get; set; } = "Release";

        /// <summary>应用服务集合</summary>
        [Description("应用服务集合")]
        public ServiceInfo[] Services { get; set; }
        #endregion

        #region 构造
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

                    AutoStart = false,
                    AutoRestart = true,
                    //RestartExistCodes = "0,1,3",
                };

                Services = new[] { si };
            }

            base.OnLoaded();
        }

        /// <summary>添加应用服务</summary>
        /// <param name="services">应用服务集合</param>
        public void Add(ServiceInfo[] services)
        {
            var list = Services.ToList();
            foreach (var item in services)
            {
                if (!list.Any(e => e.Name.EqualIgnoreCase(item.Name))) list.Add(item);
            }

            Services = list.ToArray();
        }
        #endregion
    }
}