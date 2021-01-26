using System;
using System.ComponentModel;
using NewLife;
using NewLife.Configuration;
using NewLife.Security;

namespace Stardust.Server
{
    /// <summary>配置</summary>
    [Config("StarServer")]
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

        /// <summary>节点编码公式。选择NodeInfo哪些硬件信息来计算节点编码，支持Crc/Crc16/MD5/MD5_16，默认Crc({UUID}@{MachineGuid}@{Macs})</summary>
        [Description("节点编码公式。选择NodeInfo哪些硬件信息来计算节点编码，支持Crc/Crc16/MD5/MD5_16，默认Crc({UUID}@{MachineGuid}@{Macs})")]
        public String NodeCodeFormula { get; set; } = "Crc({UUID}@{MachineGuid}@{Macs})";

        /// <summary>监控流统计。默认5秒</summary>
        [Description("监控流统计。默认5秒")]
        public Int32 MonitorFlowPeriod { get; set; } = 5;

        /// <summary>监控流统计。默认30秒</summary>
        [Description("监控批统计。默认30秒")]
        public Int32 MonitorBatchPeriod { get; set; } = 30;

        /// <summary>监控告警周期。默认30秒</summary>
        [Description("监控告警周期。默认30秒")]
        public Int32 AlarmPeriod { get; set; } = 30;

        /// <summary>控制台地址。用于监控告警地址</summary>
        [Description("控制台地址。用于监控告警地址")]
        public String WebUrl { get; set; } = "";

        /// <summary>数据保留时间。默认3天</summary>
        [Description("数据保留时间。默认3天")]
        public Int32 DataRetention { get; set; } = 3;

        /// <summary>大颗粒数据保留时间。默认30天</summary>
        [Description("大颗粒数据保留时间。默认30天")]
        public Int32 DataRetention2 { get; set; } = 30;
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