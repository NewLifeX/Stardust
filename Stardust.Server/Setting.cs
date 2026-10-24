using System.ComponentModel;
using NewLife;
using NewLife.Configuration;
using NewLife.Remoting.Models;
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

    /// <summary>界面语言。如 zh-Hans、zh-Hant、en、ja，留空时按请求语言识别，Web 之外跟随系统语言</summary>
    [Description("UI language, for example zh-Hans, zh-Hant, en, or ja; leave empty to detect from each request in web hosts and to follow the system language otherwise")]
    public String? Language { get; set; }

    /// <summary>服务端口。默认6600</summary>
    [Description("Service port. Default 6600.")]
    public Int32 Port { get; set; } = 6600;

    /// <summary>令牌密钥。用于生成JWT令牌的算法和密钥，如HS256:ABCD1234</summary>
    [Description("Token secret. Algorithm and secret used to generate JWT tokens, e.g. HS256:ABCD1234")]
    public String TokenSecret { get; set; }

    /// <summary>令牌有效期。默认2*3600秒</summary>
    [Description("Token validity period. Default 2*3600 seconds.")]
    public Int32 TokenExpire { get; set; } = 2 * 3600;

    /// <summary>会话超时。默认600秒</summary>
    [Description("Session timeout. Default 600 seconds")]
    public Int32 SessionTimeout { get; set; } = 600;

    /// <summary>盐值偏差时间。客户端登录时使用当前时间加盐，如果跟服务端时间差距过大，将会导致密钥验证失败，默认600秒</summary>
    [Description("Salt deviation time. Clients use the current time as salt when logging in; too large a clock difference from the server causes key verification to fail; default 600 seconds")]
    public Int32 SaltTime { get; set; } = 600;

    /// <summary>自动注册。允许节点客户端自动注册，默认true</summary>
    [Description("Auto-register. Allow node clients to register automatically, default true")]
    public Boolean AutoRegister { get; set; } = true;

    /// <summary>应用自动注册。允许应用客户端自动注册，默认true</summary>
    [Description("Application auto-registration. Allow app clients to register automatically; default true")]
    public Boolean AppAutoRegister { get; set; } = true;

    /// <summary>准入白名单。若指定，仅允许符合IP条件的节点进行注册，多个逗号隔开，支持*模糊匹配</summary>
    [Description("Admission whitelist. If specified, only nodes matching the IP conditions may register; separate multiple entries with commas; supports * wildcard matching")]
    public String WhiteIP { get; set; } = "";

    /// <summary>节点编码公式。选择NodeInfo哪些硬件信息来计算节点编码，支持Crc/Crc16/MD5/MD5_16，默认Crc({ProductCode}@{UUID}@{DiskID}@{Macs})</summary>
    [Description("Node code formula. Selects which hardware information in NodeInfo is used to compute the node code. Supports Crc/Crc16/MD5/MD5_16, default Crc({ProductCode}@{UUID}@{DiskID}@{Macs})")]
    public String NodeCodeFormula { get; set; } = "Crc({ProductCode}@{UUID}@{DiskID}@{Macs})";

    /// <summary>节点编码辨识度。UUID+Guid+SerialNumber+DiskId+MAC，只要其中几个相同，就认为是同一个节点，默认2</summary>
    [Description("Node code identification threshold. UUID+Guid+SerialNumber+DiskId+MAC; if several of them match, the node is considered the same, default 2")]
    public Int32 NodeCodeLevel { get; set; } = 2;

    /// <summary>监控流统计周期。默认5秒。处理内存队列计算分钟级统计，调小提高实时性但增加CPU占用，调大降低CPU但增加告警延迟</summary>
    [Description("Monitoring stream statistics period. Default 5 seconds. Processes the in-memory queue to compute minute-level statistics; smaller values improve real-time responsiveness but increase CPU usage, while larger values reduce CPU usage but increase alert latency")]
    public Int32 MonitorFlowPeriod { get; set; } = 5;

    /// <summary>监控批统计周期。默认30秒。从数据库聚合修正数据，调小提高修正速度但增加IO压力，调大降低压力但延迟修正</summary>
    [Description("Monitoring batch statistics period. Default 30 seconds. Corrects data by aggregating from the database; smaller values speed up correction but increase IO pressure, larger values reduce pressure but delay correction")]
    public Int32 MonitorBatchPeriod { get; set; } = 30;

    /// <summary>监控落盘周期。默认300秒。统计延迟队列批量提交周期，告警读取内存实时值不受影响，调大显著降低数据库UPDATE压力</summary>
    [Description("Monitoring flush period. Default 300 seconds. Batch commit period for the statistics delay queue; alerts read real-time in-memory values and are unaffected. Larger values significantly reduce database UPDATE pressure")]
    public Int32 MonitorSavePeriod { get; set; } = 300;

    /// <summary>监控巡检周期。默认600秒。非热门应用按周期重算小时/日统计，替代每个批周期的全量重算，降低写放大</summary>
    [Description("Monitoring inspection period. Default 600 seconds. Recalculates hourly/daily statistics for non-hot applications on a periodic basis instead of full recalculation in each batch cycle, reducing write amplification")]
    public Int32 MonitorPatrolPeriod { get; set; } = 600;

    /// <summary>监控告警周期。默认30秒</summary>
    [Description("Monitoring alert period. Default 30 seconds")]
    public Int32 AlarmPeriod { get; set; } = 30;

    ///// <summary>服务端地址。用于下载更新包</summary>
    //[Description("服务端地址。用于下载更新包")]
    //public String ServerUrl { get; set; } = "";

    /// <summary>控制台地址。用于监控告警地址</summary>
    [Description("Console address. Used for monitoring alert addresses")]
    public String WebUrl { get; set; } = "";

    /// <summary>数据保留时间。采样明细及分钟级统计数据，默认3天</summary>
    [Description("Data retention period. Sampled details and minute-level statistics; default 3 days.")]
    public Int32 DataRetention { get; set; } = 3;

    /// <summary>中等颗粒数据保留时间。性能数据及小时级统计数据，默认30天</summary>
    [Description("Medium-granularity data retention. Performance data and hourly statistics, default 30 days")]
    public Int32 DataRetention2 { get; set; } = 30;

    /// <summary>大颗粒数据保留时间。历史数据及每日统计数据，默认300天</summary>
    [Description("Retention period for coarse-grained data. Historical data and daily statistics, default 300 days.")]
    public Int32 DataRetention3 { get; set; } = 300;

    /// <summary>数据清理模式。支持高效Truncate，或者无需DDL的Delete，默认Truncate</summary>
    [Description("Data cleanup mode. Supports efficient Truncate or Delete without DDL. Default Truncate.")]
    public ClearModes ClearMode { get; set; } = ClearModes.Truncate;

    /// <summary>上传目录。存放升级包，需要跟StarWeb配置为同一个目录，默认../Uploads</summary>
    [Description("Upload directory. Stores upgrade packages and must be the same directory configured for StarWeb. Default ../Uploads.")]
    public String UploadPath { get; set; } = "../Uploads";

    /// <summary>上传包最大大小。仅作用于 Deploy/UploadBuildFile 接口允许的最大请求体字节数，默认100000000（100MB），0表示使用Kestrel默认限制，最大不超过1GB</summary>
    [Description("Maximum upload package size. Applies only to the maximum allowed request body bytes for the Deploy/UploadBuildFile endpoint; default 100000000 (100MB), 0 means use the Kestrel default limit, max 1GB.")]
    public Int64 MaxUploadSize { get; set; } = 100000000;

    /// <summary>文件缓存目录。存放数据库驱动等缓存文件，为空时不启用，默认../FileCache</summary>
    [Description("File cache directory. Stores cache files such as database drivers; disabled when empty; default ../FileCache")]
    public String FileCache { get; set; } = "../FileCache";

    /// <summary>文件缓存白名单。若指定，仅允许符合条件的IP来源访问文件缓存，多个逗号隔开，支持*模糊匹配</summary>
    [Description("File cache whitelist. If specified, only matching IP sources can access the file cache; separate multiple entries with commas; supports * wildcards")]
    public String FileCacheWhiteIP { get; set; } = "";

    /// <summary>文件存储提供服务。是否响应其他节点的文件下载请求，默认true</summary>
    [Description("File storage serving. Whether to respond to file download requests from other nodes. Default true.")]
    public Boolean FileStorageProvide { get; set; } = true;

    /// <summary>文件存储拉取文件。是否主动拉取其他节点发布的新文件，默认false</summary>
    [Description("File storage pull. Whether to actively pull new files published by other nodes. Default false.")]
    public Boolean FileStorageFetch { get; set; } = false;

    /// <summary>上级服务器。同步向上级汇报数据</summary>
    [Description("Upstream server. Reports data synchronously to the upstream server.")]
    public String UplinkServer { get; set; }

    /// <summary>固定城市。默认自动根据IP计算所在城市，开启后不再自动计算，改为人工设置</summary>
    [Description("Fixed city. By default, the city is calculated automatically from the IP. When enabled, automatic calculation stops and the city is set manually")]
    public Boolean FixedCity { get; set; }

    /// <summary>调用链允许匿名。允许未登录用户访问/trace调用链页面，监控数据可能含敏感信息，默认false</summary>
    [Description("Allow anonymous trace access. Allow unauthenticated users to access the /trace trace page; monitoring data may contain sensitive information. Default false.")]
    public Boolean TraceAnonymous { get; set; } = false;

    /// <summary>dotNet同步周期。从官网抓取.NET运行时安装包信息，0表示禁用，单位秒，默认43200（12小时）</summary>
    [Description("dotNet sync interval. Fetch .NET runtime installer info from the official site, 0 disables, unit: seconds, default 43200 (12 hours)")]
    public Int32 DotNetSyncPeriod { get; set; } = 43200;

    /// <summary>dotNet同步地址。抓取.NET运行时版本的官方API地址，支持{major}占位符</summary>
    [Description("dotNet sync URL. Official API URL for fetching .NET runtime versions, supports the {major} placeholder")]
    public String DotNetSyncUrl { get; set; } = "https://dotnetcli.blob.core.windows.net/dotnet/release-metadata/{major}.0/releases.json";

    /// <summary>星尘资源下载基础地址。StarAgent安装包/一键脚本等资源的基础URL，.NET运行时脚本位于其同级dotnet/目录，默认官方地址</summary>
    [Description("Stardust resource download base address. Base URL for StarAgent installers/scripts; defaults to the official address")]
    public String AgentDownloadBaseUrl { get; set; } = "https://x.newlifex.com/star/";

    ///// <summary>新服务器。节点自动迁移到新的服务器地址</summary>
    //[Description("新服务器。节点自动迁移到新的服务器地址")]
    //public String NewServer { get; set; }

    /// <summary>可控节点为空时放行。AllowControlNodes为空时是否允许操作，默认false（安全模式，需显式配置可控节点）。内部环境可设为true以兼容旧数据</summary>
    [Description("Allow when controllable nodes are empty. Whether operations are allowed when AllowControlNodes is empty, default false (secure mode, controllable nodes must be configured explicitly). Internal environments can set it to true for compatibility with legacy data")]
    public Boolean AllowControlNodesWhenEmpty { get; set; }

    /// <summary>启用MCP服务。默认关闭，启用后暴露 POST /mcp 端点供 LLM/智能体调用</summary>
    [Description("Enable the MCP service. Disabled by default; when enabled, exposes the POST /mcp endpoint for LLM/agent calls.")]
    public Boolean EnableMcp { get; set; } = false;

    /// <summary>MCP动作集。逗号分隔的模块名（node/app/config/deploy/gateway/monitor/system），*表示全部启用</summary>
    [Description("MCP action set. Comma-separated module names (node/app/config/deploy/gateway/monitor/system), * means all enabled")]
    public String McpActionSet { get; set; } = "*";

    /// <summary>限流路径前缀。逗号分隔，命中则按IP限流，默认登录/令牌等认证接口</summary>
    [Description("Rate limit path prefixes. Comma-separated; matching paths are rate-limited by IP; defaults to authentication endpoints such as login/token")]
    public String RateLimitPaths { get; set; } = "/App/Login,/Node/Login,/OAuth/Token,/App/Register";

    /// <summary>限流次数。每IP每分钟允许访问次数，默认60</summary>
    [Description("Rate limit count. Number of requests allowed per IP per minute; default 60")]
    public Int32 RateLimitTimes { get; set; } = 60;

    /// <summary>允许匿名埋点上报。多级级联（下级StarServer转发）或未配置密钥的极简SDK需要，默认false（安全）</summary>
    [Description("Allow anonymous telemetry reporting. Required for multi-level cascading or minimal SDK; default false (secure)")]
    public Boolean AnonymousReport { get; set; }

    /// <summary>CORS允许的来源。逗号分隔，为空时允许任意来源（默认兼容），指定后仅允许白名单来源跨域访问（安全V-15）</summary>
    [Description("CORS allowed origins. Comma-separated; empty allows any origin; when specified, only whitelisted origins may cross-origin")]
    public String CorsOrigins { get; set; }

    /// <summary>附件下载鉴权。是否校验 /cube/file 附件下载身份/令牌（安全V-11），默认false兼容存量，开启后需配合下载令牌签发链路</summary>
    [Description("Attachment download authentication. Whether to verify the attachment download token for /cube/file. Default false.")]
    public Boolean ValidateAttachment { get; set; }

    /// <summary>免登录公开附件分类。逗号分隔，ValidateAttachment=true时生效，命中分类的附件可匿名下载</summary>
    [Description("Public attachment categories that require no login. Comma separated; matching items can be downloaded anonymously.")]
    public String PublicAttachmentCategories { get; set; }

    /// <summary>附件下载令牌有效期。秒，默认600</summary>
    [Description("Attachment download token validity. Seconds, default 600.")]
    public Int32 AttachmentTokenExpire { get; set; } = 600;
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