using System.ComponentModel;
using NewLife.Configuration;
using NewLife.Remoting.Clients;
using Stardust.Models;

namespace StarAgent;

/// <summary>配置</summary>
[Config("StarAgent")]
public class StarAgentSetting : Config<StarAgentSetting>, IClientSetting
{
    #region 属性
    /// <summary>调试开关。默认true</summary>
    [Description("调试开关。默认true")]
    public Boolean Debug { get; set; } = true;

    /// <summary>证书</summary>
    [Description("证书")]
    public String Code { get; set; } = null!;

    /// <summary>密钥</summary>
    [Description("密钥")]
    public String? Secret { get; set; }

    /// <summary>项目名。新节点默认所需要加入的项目</summary>
    [Description("项目名。新节点默认所需要加入的项目")]
    public String? Project { get; set; }

    ///// <summary>本地服务。默认udp://127.0.0.1:5500</summary>
    //[Description("本地服务。默认udp://127.0.0.1:5500")]
    //public String LocalServer { get; set; } = "udp://127.0.0.1:5500";

    /// <summary>本地端口。默认5500</summary>
    [Description("本地端口。默认5500")]
    public Int32 LocalPort { get; set; } = 5500;

    /// <summary>更新通道。默认Release</summary>
    [Description("更新通道。默认Release")]
    public String Channel { get; set; } = "Release";

    /// <summary>Windows自启动。自启动需要用户登录桌面，默认false使用系统服务</summary>
    [Description("Windows自启动。自启动需要用户登录桌面，默认false使用系统服务")]
    public Boolean UseAutorun { get; set; }

    /// <summary>用户名称。用户模式存储，服务模式读取</summary>
    [Description("用户名称。用户模式存储，服务模式读取")]
    public String? UserName { get; set; }

    /// <summary>像素点。例如96*96。用户模式存储，服务模式读取</summary>
    [Description("像素点。例如96*96。用户模式存储，服务模式读取")]
    public String? Dpi { get; set; }

    /// <summary>分辨率。例如1024*768。用户模式存储，服务模式读取</summary>
    [Description("分辨率。例如1024*768。用户模式存储，服务模式读取")]
    public String? Resolution { get; set; }

    /// <summary>延迟时间。重启进程或服务的延迟时间，默认3000ms</summary>
    [Description("延迟时间。重启进程或服务的延迟时间，默认3000ms")]
    public Int32 Delay { get; set; } = 3000;

    /// <summary>同步时间间隔。定期同步服务器时间到本地，默认0秒不同步</summary>
    [Description("同步时间间隔。定期同步服务器时间到本地，默认0秒不同步")]
    public Int32 SyncTime { get; set; }

    /// <summary>启动挂钩。拉起目标进程时，对dotNet应用注入星尘监控钩子，默认false</summary>
    [Description("启动挂钩。拉起目标进程时，对dotNet应用注入星尘监控钩子，默认false")]
    public Boolean StartupHook { get; set; }

    /// <summary>应用服务集合</summary>
    [Description("应用服务集合")]
    public ServiceInfo[] Services { get; set; }

    String IClientSetting.Server { get; set; }
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
                FileName = "ping",
                Arguments = "newlifex.com",

                Enable = false,
            };
            var si2 = new ServiceInfo
            {
                Name = "test2",
                FileName = "cube.zip",
                Arguments = "urls=http://*:1080",
                WorkingDirectory = "../sso/web/",

                Enable = false,
            };
            var si3 = new ServiceInfo
            {
                Name = "StarServer",
                FileName = "StarServer.zip",
                Arguments = "StarServer.dll",
                WorkingDirectory = "../star/server",

                Enable = false,
            };
            var si4 = new ServiceInfo
            {
                Name = "StarWeb",
                FileName = "StarWeb.zip",
                Arguments = "urls=http://*:6680",
                WorkingDirectory = "../star/web",

                Enable = false,
            };

            Services = [si, si2, si3, si4];
        }

        base.OnLoaded();
    }
    #endregion
}