namespace Stardust.Models
{
    /// <summary>应用心跳信息</summary>
    public class AppPingInfo
    {
        /// <summary>应用标识</summary>
        public String AppId { get; set; }

        /// <summary>应用名</summary>
        public String AppName { get; set; }

        /// <summary>实例。应用可能多实例部署，ip@proccessid</summary>
        public String ClientId { get; set; }

        /// <summary>版本</summary>
        public String Version { get; set; }

        /// <summary>应用信息</summary>
        public AppInfo Info { get; set; }
    }
}