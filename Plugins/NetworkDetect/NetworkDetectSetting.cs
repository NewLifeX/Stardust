using System.ComponentModel;
using NewLife.Configuration;

namespace NetworkDetect;

/// <summary>网络监测设置</summary>
[Config("NetworkDetect")]
public class NetworkDetectSetting : Config<NetworkDetectSetting>
{
    #region 属性
    /// <summary>调试。默认启用</summary>
    [Description("调试。默认启用")]
    public Boolean Debug { get; set; } = true;

    /// <summary>周期。检测周期，默认5秒</summary>
    [Description("周期。检测周期，默认5秒")]
    public Int32 Period { get; set; } = 5;

    /// <summary>服务集合</summary>
    [Description("服务集合")]
    public ServiceItem[] Services { get; set; }
    #endregion

    #region 方法
    /// <summary>加载完成后</summary>
    protected override void OnLoaded()
    {
        if (Services == null || Services.Length == 0)
        {
            var si = new ServiceItem
            {
                Name = "路由心跳",
                Address = "192.168.1.1",
            };
            var si2 = new ServiceItem
            {
                Name = "交换机",
                Address = "192.168.1.254",
            };

            Services = new[] { si, si2 };
        }

        base.OnLoaded();
    }
    #endregion
}