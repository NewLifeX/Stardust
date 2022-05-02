namespace Stardust.Models
{
    /// <summary>应用模型</summary>
    public class AppModel
    {
        /// <summary>应用标识</summary>
        public String AppId { get; set; }

        /// <summary>应用名</summary>
        public String AppName { get; set; }

        /// <summary>版本</summary>
        public String Version { get; set; }

        /// <summary>实例。应用可能多实例部署，ip@proccessid</summary>
        public String ClientId { get; set; }

        /// <summary>本地IP地址。随着网卡变动，可能改变</summary>
        public String IP { get; set; }

        /// <summary>节点编码</summary>
        public String NodeCode { get; set; }
    }
}