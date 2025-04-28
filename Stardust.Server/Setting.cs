using System.ComponentModel;
using NewLife;
using NewLife.Configuration;
using NewLife.Remoting.Extensions.Models;
using NewLife.Security;
using XCode.Configuration;

namespace Stardust.Server;

/// <summary>数据清理模式</summary>
public enum ClearModes
{
    /// <summary>清空表。高效，不产生binlog日志，需要DDL权限</summary>
    Truncate = 1,

    /// <summary>删除数据。效率较低，产生binlog日志，无需DDL权限</summary>
    Delete = 2,
}

/// <summary>配置</summary>
[Config("StarServer")]
public class StarServerSetting : Config<StarServerSetting>, ITokenSetting
{
    #region 静态
    static StarServerSetting() => Provider = new DbConfigProvider { UserId = 0, Category = "StarServer" };
    #endregion

    #region 属性
    ///// <summary>调试开关。默认true</summary>
    //[Description("调试开关。默认true")]
    //public Boolean Debug { get; set; } = true;

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

    /// <summary>自动注册。允许节点客户端自动注册，默认true</summary>
    [Description("自动注册。允许节点客户端自动注册，默认true")]
    public Boolean AutoRegister { get; set; } = true;

    /// <summary>应用自动注册。允许应用客户端自动注册，默认true</summary>
    [Description("应用自动注册。允许应用客户端自动注册，默认true")]
    public Boolean AppAutoRegister { get; set; } = true;

    /// <summary>准入白名单。若指定，仅允许符合IP条件的节点进行注册，多个逗号隔开，支持*模糊匹配</summary>
    [Description("准入白名单。若指定，仅允许符合IP条件的节点进行注册，多个逗号隔开，支持*模糊匹配")]
    public String WhiteIP { get; set; } = "";

    /// <summary>节点编码公式。选择NodeInfo哪些硬件信息来计算节点编码，支持Crc/Crc16/MD5/MD5_16，默认Crc({ProductCode}@{UUID}@{DiskID}@{Macs})</summary>
    [Description("节点编码公式。选择NodeInfo哪些硬件信息来计算节点编码，支持Crc/Crc16/MD5/MD5_16，默认Crc({ProductCode}@{UUID}@{DiskID}@{Macs})")]
    public String NodeCodeFormula { get; set; } = "Crc({ProductCode}@{UUID}@{DiskID}@{Macs})";

    /// <summary>节点编码辨识度。UUID+Guid+SerialNumber+DiskId+MAC，只要其中几个相同，就认为是同一个节点，默认2</summary>
    [Description("节点编码辨识度。UUID+Guid+SerialNumber+DiskId+MAC，只要其中几个相同，就认为是同一个节点，默认2")]
    public Int32 NodeCodeLevel { get; set; } = 2;

    /// <summary>监控流统计。默认5秒</summary>
    [Description("监控流统计。默认5秒")]
    public Int32 MonitorFlowPeriod { get; set; } = 5;

    /// <summary>监控流统计。默认30秒</summary>
    [Description("监控批统计。默认30秒")]
    public Int32 MonitorBatchPeriod { get; set; } = 30;

    /// <summary>监控告警周期。默认30秒</summary>
    [Description("监控告警周期。默认30秒")]
    public Int32 AlarmPeriod { get; set; } = 30;

    ///// <summary>服务端地址。用于下载更新包</summary>
    //[Description("服务端地址。用于下载更新包")]
    //public String ServerUrl { get; set; } = "";

    /// <summary>控制台地址。用于监控告警地址</summary>
    [Description("控制台地址。用于监控告警地址")]
    public String WebUrl { get; set; } = "";

    /// <summary>数据保留时间。采样明细及分钟级统计数据，默认3天</summary>
    [Description("数据保留时间。采样明细及分钟级统计数据，默认3天")]
    public Int32 DataRetention { get; set; } = 3;

    /// <summary>中等颗粒数据保留时间。性能数据及小时级统计数据，默认30天</summary>
    [Description("中等颗粒数据保留时间。性能数据及小时级统计数据，默认30天")]
    public Int32 DataRetention2 { get; set; } = 30;

    /// <summary>大颗粒数据保留时间。历史数据及每日统计数据，默认300天</summary>
    [Description("大颗粒数据保留时间。历史数据及每日统计数据，默认300天")]
    public Int32 DataRetention3 { get; set; } = 300;

    /// <summary>数据清理模式。支持高效Truncate，或者无需DDL的Delete，默认Truncate</summary>
    [Description("数据清理模式。支持高效Truncate，或者无需DDL的Delete，默认Truncate")]
    public ClearModes ClearMode { get; set; } = ClearModes.Truncate;

    /// <summary>上传目录。存放升级包，需要跟StarWeb配置为同一个目录，默认../Uploads</summary>
    [Description("上传目录。存放升级包，需要跟StarWeb配置为同一个目录，默认../Uploads")]
    public String UploadPath { get; set; } = "../Uploads";

    /// <summary>文件缓存目录。存放数据库驱动等缓存文件，为空时不启用，默认../FileCache</summary>
    [Description("文件缓存目录。存放数据库驱动等缓存文件，为空时不启用，默认../FileCache")]
    public String FileCache { get; set; } = "../FileCache";

    /// <summary>文件缓存白名单。若指定，仅允许符合条件的IP来源访问文件缓存，多个逗号隔开，支持*模糊匹配</summary>
    [Description("文件缓存白名单。若指定，仅允许符合条件的IP来源访问文件缓存，多个逗号隔开，支持*模糊匹配")]
    public String FileCacheWhiteIP { get; set; } = "";

    /// <summary>上级服务器。同步向上级汇报数据</summary>
    [Description("上级服务器。同步向上级汇报数据")]
    public String UplinkServer { get; set; }

    ///// <summary>新服务器。节点自动迁移到新的服务器地址</summary>
    //[Description("新服务器。节点自动迁移到新的服务器地址")]
    //public String NewServer { get; set; }
    #endregion

    #region 方法
    /// <summary>加载时触发</summary>
    protected override void OnLoaded()
    {
        if (TokenSecret.IsNullOrEmpty() || TokenSecret.Split(':').Length != 2) TokenSecret = $"HS256:{Rand.NextString(16)}";

        if (NodeCodeFormula.IsNullOrEmpty() || NodeCodeFormula == "Crc({UUID}@{MachineGuid}@{Macs})")
            NodeCodeFormula = "Crc({ProductCode}@{UUID}@{DiskID}@{Macs})";

        base.OnLoaded();
    }
    #endregion
}