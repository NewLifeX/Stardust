namespace Stardust.Web.Models.Dashboard;

/// <summary>看板页面公共字段</summary>
public class DashboardPageModel
{
    /// <summary>页面标识。system/project/app/node/wallboard</summary>
    public String Page { get; set; }

    /// <summary>可见数据范围</summary>
    public ScopeInfo Scope { get; set; } = new();

    /// <summary>服务端时间</summary>
    public DateTime ServerTime { get; set; } = DateTime.Now;

    /// <summary>时间范围。1h/24h/7d</summary>
    public String Range { get; set; } = "24h";

    /// <summary>自动刷新地址</summary>
    public String DataUrl => $"/Dashboard/Data?page={Page}&range={Range}&id={Id}";

    /// <summary>当前对象编号</summary>
    public Int32 Id { get; set; }

    /// <summary>是否无可见数据</summary>
    public Boolean IsEmpty => Scope.IsNone;
}

/// <summary>系统级看板</summary>
public class SystemDashboardModel : DashboardPageModel
{
    /// <summary>KPI 指标</summary>
    public List<KpiModel> Kpis { get; set; } = [];

    /// <summary>全局吞吐与错误率</summary>
    public TrendModel Trend { get; set; } = new();

    /// <summary>集群健康</summary>
    public ClusterHealth Health { get; set; } = new();

    /// <summary>项目健康矩阵</summary>
    public List<ProjectRow> Projects { get; set; } = [];

    /// <summary>Top 应用</summary>
    public List<TopAppItem> TopApps { get; set; } = [];

    /// <summary>项目 → 应用拓扑</summary>
    public TopologyModel Topology { get; set; } = new();

    /// <summary>资源水位</summary>
    public List<RegionRow> Regions { get; set; } = [];

    /// <summary>资源分组维度。机房 / 项目</summary>
    public String RegionDimension { get; set; } = "机房";

    /// <summary>实时事件</summary>
    public List<EventItem> Events { get; set; } = [];

    /// <summary>覆盖的应用数</summary>
    public Int32 TotalApps { get; set; }

    /// <summary>覆盖的节点数</summary>
    public Int32 TotalNodes { get; set; }

    /// <summary>当前请求速率</summary>
    public Double CurrentQps { get; set; }
}

/// <summary>项目看板</summary>
public class ProjectDashboardModel : DashboardPageModel
{
    /// <summary>项目信息</summary>
    public ProjectInfo Project { get; set; } = new();

    /// <summary>KPI 指标</summary>
    public List<KpiModel> Kpis { get; set; } = [];

    /// <summary>应用矩阵</summary>
    public List<AppCardModel> Apps { get; set; } = [];

    /// <summary>节点资源</summary>
    public List<NodeResourceModel> Nodes { get; set; } = [];

    /// <summary>项目 → 应用 → 节点拓扑</summary>
    public TopologyModel Topology { get; set; } = new();

    /// <summary>最近部署</summary>
    public List<DeployItem> Deploys { get; set; } = [];

    /// <summary>当前告警</summary>
    public List<AlarmItem> Alarms { get; set; } = [];

    /// <summary>是否尚未接入应用</summary>
    public Boolean NoApps => Apps.Count == 0;
}

/// <summary>应用看板</summary>
public class AppDashboardModel : DashboardPageModel
{
    /// <summary>应用信息</summary>
    public AppInfo Info { get; set; } = new();

    /// <summary>KPI 指标</summary>
    public List<KpiModel> Kpis { get; set; } = [];

    /// <summary>请求量与错误率趋势</summary>
    public TrendModel Trend { get; set; } = new();

    /// <summary>实例健康</summary>
    public InstanceHealth Instances { get; set; } = new();

    /// <summary>端点 Top</summary>
    public List<EndpointItem> Endpoints { get; set; } = [];

    /// <summary>错误最多的端点</summary>
    public List<EndpointItem> ErrorEndpoints { get; set; } = [];

    /// <summary>错误总数</summary>
    public Int64 ErrorTotal { get; set; }

    /// <summary>运行时性能</summary>
    public MeterModel Meter { get; set; } = new();

    /// <summary>依赖与注册服务</summary>
    public List<DependencyItem> Dependencies { get; set; } = [];

    /// <summary>最近变更</summary>
    public List<ChangeItem> Changes { get; set; } = [];

    /// <summary>最新告警</summary>
    public List<AlarmItem> Alarms { get; set; } = [];
}

/// <summary>节点看板</summary>
public class NodeDashboardModel : DashboardPageModel
{
    /// <summary>节点信息</summary>
    public NodeInfo Info { get; set; } = new();

    /// <summary>KPI 指标</summary>
    public List<KpiModel> Kpis { get; set; } = [];

    /// <summary>资源时序</summary>
    public NodeSeriesModel Series { get; set; } = new();

    /// <summary>健康评分</summary>
    public RadarModel Radar { get; set; } = new();

    /// <summary>网络质量</summary>
    public NetworkModel Network { get; set; } = new();

    /// <summary>磁盘分区</summary>
    public List<DiskItem> Disks { get; set; } = [];

    /// <summary>进程守护</summary>
    public List<ProcessItem> Processes { get; set; } = [];

    /// <summary>进程数</summary>
    public Int32 ProcessCount { get; set; }

    /// <summary>TCP 连接数</summary>
    public Int32 TcpConnections { get; set; }

    /// <summary>已部署应用</summary>
    public List<DeployNodeItem> Deploys { get; set; } = [];

    /// <summary>操作历史</summary>
    public List<HistoryItem> History { get; set; } = [];

    /// <summary>节点告警</summary>
    public List<AlarmItem> Alarms { get; set; } = [];

    /// <summary>最后上报时间</summary>
    public DateTime LastReport { get; set; }

    /// <summary>离线时长文本</summary>
    public String OfflineText { get; set; }

    /// <summary>当前使用率快照，供自动刷新更新水位条</summary>
    public Double Cpu { get; set; }

    /// <summary>内存使用率</summary>
    public Double Memory { get; set; }

    /// <summary>磁盘使用率</summary>
    public Double Disk { get; set; }
}

/// <summary>大屏模式</summary>
public class WallboardModel : DashboardPageModel
{
    /// <summary>节点在线率百分比</summary>
    public Double NodeOnlineRate { get; set; }

    /// <summary>在线节点</summary>
    public Int32 NodesOnline { get; set; }

    /// <summary>节点总数</summary>
    public Int32 NodesTotal { get; set; }

    /// <summary>资源告警节点数</summary>
    public Int32 NodesWarn { get; set; }

    /// <summary>离线节点数</summary>
    public Int32 NodesOffline { get; set; }

    /// <summary>节点在线率历史。近 24 小时，按小时取样</summary>
    public List<Double> NodeRateHistory { get; set; } = [];

    /// <summary>今日请求量</summary>
    public Int64 TodayRequests { get; set; }

    /// <summary>峰值 QPS</summary>
    public Double PeakQps { get; set; }

    /// <summary>谷值 QPS</summary>
    public Double ValleyQps { get; set; }

    /// <summary>错误率百分比</summary>
    public Double ErrorRate { get; set; }

    /// <summary>错误率环比</summary>
    public Double ErrorRateDelta { get; set; }

    /// <summary>活跃告警数</summary>
    public Int32 Alarms { get; set; }

    /// <summary>实时 QPS</summary>
    public Double CurrentQps { get; set; }

    /// <summary>平均耗时毫秒</summary>
    public Int32 AvgCost { get; set; }

    /// <summary>在线应用数</summary>
    public Int32 AppsOnline { get; set; }

    /// <summary>应用总数</summary>
    public Int32 AppsTotal { get; set; }

    /// <summary>今日部署次数</summary>
    public Int32 TodayDeploys { get; set; }

    /// <summary>项目健康列表</summary>
    public List<ProjectRow> Projects { get; set; } = [];

    /// <summary>事件流</summary>
    public List<EventItem> Events { get; set; } = [];

    /// <summary>Top 应用</summary>
    public List<TopAppItem> TopApps { get; set; } = [];

    /// <summary>资源水位</summary>
    public List<RegionRow> Regions { get; set; } = [];

    /// <summary>资源分组维度</summary>
    public String RegionDimension { get; set; } = "机房";

    /// <summary>吞吐趋势</summary>
    public TrendModel Trend { get; set; } = new();

    /// <summary>告警分布。按类别</summary>
    public List<NameCount> AlarmByCategory { get; set; } = [];

    /// <summary>告警总数分解</summary>
    public Int32 AlarmAlarming { get; set; }

    /// <summary>已恢复告警</summary>
    public Int32 AlarmRecovered { get; set; }

    /// <summary>今日触发次数</summary>
    public Int32 AlarmToday { get; set; }
}

/// <summary>列表选择页。应用 / 节点 / 项目</summary>
public class PickerPageModel : DashboardPageModel
{
    /// <summary>页面标题</summary>
    public String Title { get; set; }

    /// <summary>说明</summary>
    public String Subtitle { get; set; }

    /// <summary>项目列表</summary>
    public List<ProjectRow> Projects { get; set; } = [];

    /// <summary>应用列表</summary>
    public List<AppCardModel> Apps { get; set; } = [];

    /// <summary>节点列表</summary>
    public List<NodeResourceModel> Nodes { get; set; } = [];
}
