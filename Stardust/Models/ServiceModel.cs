﻿using System;

namespace Stardust.Models
{
    /// <summary>服务模型</summary>
    public class ServiceModel
    {
        /// <summary>服务名</summary>
        public String ServiceName { get; set; }

        /// <summary>客户端。IP加进程</summary>
        public String Client { get; set; }

        /// <summary>版本</summary>
        public String Version { get; set; }

        /// <summary>地址</summary>
        public String Address { get; set; }

        /// <summary>权重</summary>
        public Int32 Weight { get; set; }

        /// <summary>最后活跃时间</summary>
        public DateTime LastActive { get; set; }
    }
}