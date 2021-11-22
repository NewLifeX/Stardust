using System;
using System.Collections.Generic;

namespace Stardust.Server.Models
{
    /// <summary>配置入参</summary>
    public class SetConfigModel
    {
        /// <summary>应用</summary>
        public String AppId { get; set; }

        /// <summary>密钥</summary>
        public String Secret { get; set; }

        /// <summary>配置数据</summary>
        public IDictionary<String, Object> Configs { get; set; }
    }
}