namespace Stardust.Web.Models.Dashboard;

/// <summary>可见数据范围。严格模式下由当前用户角色与项目成员关系决定</summary>
public class ScopeInfo
{
    /// <summary>管理员。系统角色可见全部项目</summary>
    public Boolean IsAdmin { get; set; }

    /// <summary>范围模式。full/limited/none</summary>
    public String Mode { get; set; } = "full";

    /// <summary>项目总数</summary>
    public Int32 TotalProjects { get; set; }

    /// <summary>可见项目数</summary>
    public Int32 VisibleProjects { get; set; }

    /// <summary>可见项目编号</summary>
    public Int32[] ProjectIds { get; set; } = [];

    /// <summary>可见范围内的项目选项，供顶栏切换</summary>
    public List<ProjectOption> Projects { get; set; } = [];

    /// <summary>徽标文本</summary>
    public String Badge => Mode == "none" ? "无可见项目" : IsAdmin ? $"全部 {TotalProjects} 个项目" : $"可见 {VisibleProjects} / {TotalProjects} 个项目";

    /// <summary>是否受限</summary>
    public Boolean IsLimited => Mode == "limited";

    /// <summary>是否完全无授权</summary>
    public Boolean IsNone => Mode == "none";
}

/// <summary>项目选项</summary>
public class ProjectOption
{
    /// <summary>项目编号</summary>
    public Int32 Id { get; set; }

    /// <summary>项目名称</summary>
    public String Name { get; set; }

    /// <summary>看板地址</summary>
    public String Url => $"/Dashboard/Project/{Id}";
}

/// <summary>KPI 指标卡</summary>
public class KpiModel
{
    /// <summary>键。自动刷新按此键定位</summary>
    public String Key { get; set; }

    /// <summary>标题</summary>
    public String Label { get; set; }

    /// <summary>主数值</summary>
    public String Value { get; set; }

    /// <summary>单位</summary>
    public String Unit { get; set; }

    /// <summary>脚注说明</summary>
    public String Note { get; set; }

    /// <summary>环比/趋势文本</summary>
    public String Delta { get; set; }

    /// <summary>趋势等级。good/bad/flat/warn</summary>
    public String DeltaLevel { get; set; }

    /// <summary>配色。brand/online/alert/offline/accent</summary>
    public String Tone { get; set; }

    /// <summary>迷你趋势数据</summary>
    public Double[] Spark { get; set; }

    /// <summary>迷你趋势颜色。空则跟随色调</summary>
    public String SparkColor { get; set; }

    /// <summary>点击跳转地址</summary>
    public String Url { get; set; }
}

/// <summary>趋势序列</summary>
public class TrendModel
{
    /// <summary>横轴标签</summary>
    public String[] Categories { get; set; } = [];

    /// <summary>主序列。请求量，单位 QPS</summary>
    public Double[] Primary { get; set; } = [];

    /// <summary>错误率百分比</summary>
    public Double[] ErrorRate { get; set; } = [];

    /// <summary>主序列名称</summary>
    public String PrimaryName { get; set; } = "请求量";

    /// <summary>主序列单位</summary>
    public String PrimaryUnit { get; set; } = "QPS";
}

/// <summary>集群健康</summary>
public class ClusterHealth
{
    /// <summary>在线节点</summary>
    public Int32 Online { get; set; }

    /// <summary>告警节点。资源超阈值</summary>
    public Int32 Warn { get; set; }

    /// <summary>离线节点</summary>
    public Int32 Offline { get; set; }

    /// <summary>在线率百分比</summary>
    public Double Rate { get; set; }

    /// <summary>离线/告警节点提示</summary>
    public List<EventItem> Items { get; set; } = [];
}

/// <summary>项目健康行</summary>
public class ProjectRow
{
    /// <summary>项目编号</summary>
    public Int32 Id { get; set; }

    /// <summary>项目名称</summary>
    public String Name { get; set; }

    /// <summary>应用总数</summary>
    public Int32 Apps { get; set; }

    /// <summary>在线应用数</summary>
    public Int32 AppsOnline { get; set; }

    /// <summary>节点总数</summary>
    public Int32 Nodes { get; set; }

    /// <summary>在线节点数</summary>
    public Int32 NodesOnline { get; set; }

    /// <summary>今日错误率百分比</summary>
    public Double ErrorRate { get; set; }

    /// <summary>活跃告警数</summary>
    public Int32 Alarms { get; set; }

    /// <summary>健康度。0-100</summary>
    public Int32 Health { get; set; }

    /// <summary>看板地址</summary>
    public String Url => $"/Dashboard/Project/{Id}";
}

/// <summary>Top 应用</summary>
public class TopAppItem
{
    /// <summary>应用编号</summary>
    public Int32 Id { get; set; }

    /// <summary>项目编号</summary>
    public Int32 ProjectId { get; set; }

    /// <summary>应用名称</summary>
    public String Name { get; set; }

    /// <summary>每秒请求数</summary>
    public Double Qps { get; set; }

    /// <summary>错误率百分比</summary>
    public Double ErrorRate { get; set; }

    /// <summary>平均耗时毫秒</summary>
    public Int32 Cost { get; set; }

    /// <summary>看板地址</summary>
    public String Url => $"/Dashboard/App/{Id}";
}

/// <summary>拓扑图节点</summary>
public class TopologyNode
{
    /// <summary>节点键</summary>
    public String Key { get; set; }

    /// <summary>显示名称</summary>
    public String Name { get; set; }

    /// <summary>分类索引</summary>
    public Int32 Category { get; set; }

    /// <summary>坐标 X。0-1 归一化</summary>
    public Double X { get; set; }

    /// <summary>坐标 Y。0-1 归一化</summary>
    public Double Y { get; set; }

    /// <summary>节点大小</summary>
    public Int32 SymbolSize { get; set; } = 28;

    /// <summary>数值。QPS 或实例数</summary>
    public Double Value { get; set; }

    /// <summary>点击地址</summary>
    public String Url { get; set; }
}

/// <summary>拓扑图连线</summary>
public class TopologyLink
{
    /// <summary>起点</summary>
    public String Source { get; set; }

    /// <summary>终点</summary>
    public String Target { get; set; }

    /// <summary>连线权重</summary>
    public Double Value { get; set; }
}

/// <summary>拓扑图</summary>
public class TopologyModel
{
    /// <summary>分类名称</summary>
    public List<String> Categories { get; set; } = [];

    /// <summary>节点</summary>
    public List<TopologyNode> Nodes { get; set; } = [];

    /// <summary>连线</summary>
    public List<TopologyLink> Links { get; set; } = [];

    /// <summary>是否为空</summary>
    public Boolean IsEmpty => Nodes.Count == 0;
}

/// <summary>资源水位行。按机房或项目聚合</summary>
public class RegionRow
{
    /// <summary>分组名称</summary>
    public String Name { get; set; }

    /// <summary>项目编号。按项目分组时有值，按机房分组时为 0</summary>
    public Int32 ProjectId { get; set; }

    /// <summary>节点数</summary>
    public Int32 Nodes { get; set; }

    /// <summary>平均 CPU 使用率</summary>
    public Double Cpu { get; set; }

    /// <summary>平均内存使用率</summary>
    public Double Memory { get; set; }

    /// <summary>平均磁盘使用率</summary>
    public Double Disk { get; set; }

    /// <summary>下钻地址。按项目分组时进入项目节点列表，否则进入节点列表</summary>
    public String Url => DashboardLinks.NodeList(ProjectId);
}

/// <summary>事件流条目</summary>
public class EventItem
{
    /// <summary>发生时间</summary>
    public DateTime Time { get; set; }

    /// <summary>等级。ok/warn/bad/info</summary>
    public String Level { get; set; } = "info";

    /// <summary>标题</summary>
    public String Title { get; set; }

    /// <summary>补充说明</summary>
    public String Meta { get; set; }

    /// <summary>时间文本</summary>
    public String TimeText => Time.Year > 2000 ? Time.ToString("HH:mm") : "—";

    /// <summary>跳转地址</summary>
    public String Url { get; set; }

    /// <summary>告警编号。仅告警事件有值，用于快捷操作</summary>
    public Int64 AlarmId { get; set; }

    /// <summary>是否已确认</summary>
    public Boolean Acked { get; set; }

    /// <summary>是否处于静默期</summary>
    public Boolean Silenced { get; set; }

    /// <summary>确认状态文本</summary>
    public String AckText { get; set; }

    /// <summary>静默截止时间</summary>
    public DateTime SilenceTime { get; set; }

    /// <summary>静默状态文本</summary>
    public String SilenceText { get; set; }
}

/// <summary>告警条目</summary>
public class AlarmItem
{
    /// <summary>告警编号</summary>
    public Int64 Id { get; set; }

    /// <summary>告警对象</summary>
    public String Target { get; set; }

    /// <summary>告警类别</summary>
    public String Category { get; set; }

    /// <summary>开始时间</summary>
    public DateTime StartTime { get; set; }

    /// <summary>持续秒数</summary>
    public Int32 Duration { get; set; }

    /// <summary>触发次数</summary>
    public Int32 Times { get; set; }

    /// <summary>是否已恢复</summary>
    public Boolean Recovered { get; set; }

    /// <summary>告警内容摘要</summary>
    public String Content { get; set; }

    /// <summary>确认时间</summary>
    public DateTime AckTime { get; set; }

    /// <summary>确认人</summary>
    public String AckUser { get; set; }

    /// <summary>静默截止时间</summary>
    public DateTime SilenceTime { get; set; }

    /// <summary>是否已确认</summary>
    public Boolean IsAcked => AckTime.Year > 2000;

    /// <summary>是否处于静默期</summary>
    public Boolean IsSilenced => SilenceTime > DateTime.Now;

    /// <summary>是否允许快捷操作</summary>
    public Boolean CanAct => !Recovered;

    /// <summary>告警详情地址</summary>
    public String Url => DashboardLinks.AlarmDetail(Id);

    /// <summary>持续时长文本</summary>
    public String DurationText => DashboardMath.Duration(Duration);

    /// <summary>开始时间文本</summary>
    public String StartText => StartTime.Year > 2000 ? StartTime.ToString("MM-dd HH:mm") : "—";
}

/// <summary>告警快捷操作视图模型</summary>
public class AlarmActionModel
{
    /// <summary>告警编号</summary>
    public Int64 Id { get; set; }

    /// <summary>是否已恢复</summary>
    public Boolean Recovered { get; set; }

    /// <summary>是否已确认</summary>
    public Boolean Acked { get; set; }

    /// <summary>是否处于静默期</summary>
    public Boolean Silenced { get; set; }

    /// <summary>静默截止时间</summary>
    public DateTime SilenceTime { get; set; }
}

/// <summary>应用卡片</summary>
public class AppCardModel
{
    /// <summary>应用编号</summary>
    public Int32 Id { get; set; }

    /// <summary>应用标识</summary>
    public String Name { get; set; }

    /// <summary>显示名</summary>
    public String DisplayName { get; set; }

    /// <summary>状态。online/offline/warn</summary>
    public String Status { get; set; } = "offline";

    /// <summary>在线实例数</summary>
    public Int32 Instances { get; set; }

    /// <summary>每秒请求数</summary>
    public Double Qps { get; set; }

    /// <summary>错误率百分比</summary>
    public Double ErrorRate { get; set; }

    /// <summary>平均耗时毫秒</summary>
    public Int32 Cost { get; set; }

    /// <summary>版本</summary>
    public String Version { get; set; }

    /// <summary>迷你趋势</summary>
    public Double[] Spark { get; set; }

    /// <summary>看板地址</summary>
    public String Url => $"/Dashboard/App/{Id}";
}

/// <summary>节点资源行</summary>
public class NodeResourceModel
{
    /// <summary>节点编号</summary>
    public Int32 Id { get; set; }

    /// <summary>节点名称</summary>
    public String Name { get; set; }

    /// <summary>IP 地址</summary>
    public String Ip { get; set; }

    /// <summary>机房/位置</summary>
    public String Location { get; set; }

    /// <summary>在线状态</summary>
    public Boolean Online { get; set; }

    /// <summary>CPU 使用率</summary>
    public Double Cpu { get; set; }

    /// <summary>内存使用率</summary>
    public Double Memory { get; set; }

    /// <summary>磁盘使用率</summary>
    public Double Disk { get; set; }

    /// <summary>看板地址</summary>
    public String Url => $"/Dashboard/Node/{Id}";
}

/// <summary>项目信息</summary>
public class ProjectInfo
{
    /// <summary>项目编号</summary>
    public Int32 Id { get; set; }

    /// <summary>项目名称</summary>
    public String Name { get; set; }

    /// <summary>负责人</summary>
    public String Manager { get; set; }

    /// <summary>成员数</summary>
    public Int32 Members { get; set; }

    /// <summary>创建时间</summary>
    public DateTime CreateTime { get; set; }

    /// <summary>是否启用</summary>
    public Boolean Enable { get; set; }

    /// <summary>备注</summary>
    public String Remark { get; set; }

    /// <summary>数据范围说明</summary>
    public String DataScope { get; set; }

    /// <summary>创建时间文本</summary>
    public String CreateText => CreateTime.Year > 2000 ? CreateTime.ToString("yyyy-MM-dd") : "—";
}

/// <summary>部署记录</summary>
public class DeployItem
{
    /// <summary>应用名称</summary>
    public String App { get; set; }

    /// <summary>版本</summary>
    public String Version { get; set; }

    /// <summary>动作</summary>
    public String Action { get; set; }

    /// <summary>是否成功</summary>
    public Boolean Success { get; set; }

    /// <summary>发生时间</summary>
    public DateTime Time { get; set; }

    /// <summary>时间文本</summary>
    public String TimeText => Time.Year > 2000 ? Time.ToString("MM-dd HH:mm") : "—";
}

/// <summary>应用信息</summary>
public class AppInfo
{
    /// <summary>应用编号</summary>
    public Int32 Id { get; set; }

    /// <summary>应用标识</summary>
    public String Name { get; set; }

    /// <summary>显示名</summary>
    public String DisplayName { get; set; }

    /// <summary>项目编号</summary>
    public Int32 ProjectId { get; set; }

    /// <summary>项目名称</summary>
    public String ProjectName { get; set; }

    /// <summary>分类</summary>
    public String Category { get; set; }

    /// <summary>版本</summary>
    public String Version { get; set; }

    /// <summary>编译时间</summary>
    public DateTime Compile { get; set; }

    /// <summary>是否启用</summary>
    public Boolean Enable { get; set; }

    /// <summary>是否在线</summary>
    public Boolean Online { get; set; }

    /// <summary>实例数</summary>
    public Int32 Instances { get; set; }

    /// <summary>编译时间文本</summary>
    public String CompileText => Compile.Year > 2000 ? Compile.ToString("yyyy-MM-dd HH:mm") : "—";
}

/// <summary>实例健康</summary>
public class InstanceHealth
{
    /// <summary>在线实例数</summary>
    public Int32 Online { get; set; }

    /// <summary>实例总数</summary>
    public Int32 Total { get; set; }

    /// <summary>在线实例平均在线小时数</summary>
    public Double AvgUptime { get; set; }

    /// <summary>版本分布</summary>
    public List<NameCount> Versions { get; set; } = [];

    /// <summary>实例明细</summary>
    public List<InstanceRow> Rows { get; set; } = [];
}

/// <summary>名称计数</summary>
public class NameCount
{
    /// <summary>名称</summary>
    public String Name { get; set; }

    /// <summary>计数</summary>
    public Int32 Count { get; set; }

    /// <summary>附加说明</summary>
    public String Text { get; set; }
}

/// <summary>应用实例行</summary>
public class InstanceRow
{
    /// <summary>会话标识</summary>
    public String Client { get; set; }

    /// <summary>节点名称</summary>
    public String NodeName { get; set; }

    /// <summary>节点编号</summary>
    public Int32 NodeId { get; set; }

    /// <summary>版本</summary>
    public String Version { get; set; }

    /// <summary>IP 地址</summary>
    public String Ip { get; set; }

    /// <summary>已在线小时数</summary>
    public Double Uptime { get; set; }

    /// <summary>CPU 使用率</summary>
    public Double Cpu { get; set; }

    /// <summary>内存。MB</summary>
    public Int32 Memory { get; set; }

    /// <summary>线程数</summary>
    public Int32 Threads { get; set; }

    /// <summary>连接数</summary>
    public Int32 Connections { get; set; }

    /// <summary>节点看板地址</summary>
    public String Url => NodeId > 0 ? $"/Dashboard/Node/{NodeId}" : null;
}

/// <summary>端点统计行</summary>
public class EndpointItem
{
    /// <summary>端点名称</summary>
    public String Name { get; set; }

    /// <summary>调用次数</summary>
    public Int64 Total { get; set; }

    /// <summary>错误数</summary>
    public Int64 Errors { get; set; }

    /// <summary>错误率百分比</summary>
    public Double ErrorRate { get; set; }

    /// <summary>平均耗时毫秒</summary>
    public Int32 Cost { get; set; }

    /// <summary>最大耗时毫秒</summary>
    public Int32 MaxCost { get; set; }
}

/// <summary>运行时性能</summary>
public class MeterModel
{
    /// <summary>采样时间</summary>
    public DateTime SampleTime { get; set; }

    /// <summary>CPU 使用率</summary>
    public Double Cpu { get; set; }

    /// <summary>物理内存。MB</summary>
    public Int32 Memory { get; set; }

    /// <summary>托管堆。MB</summary>
    public Int32 Heap { get; set; }

    /// <summary>线程数</summary>
    public Int32 Threads { get; set; }

    /// <summary>连接数</summary>
    public Int32 Connections { get; set; }

    /// <summary>GC 次数</summary>
    public Int32 GcCount { get; set; }

    /// <summary>采样时间文本</summary>
    public String SampleText => SampleTime.Year > 2000 ? SampleTime.ToString("HH:mm:ss") : "—";
}

/// <summary>依赖服务行</summary>
public class DependencyItem
{
    /// <summary>服务名</summary>
    public String Service { get; set; }

    /// <summary>方向。provider/consumer</summary>
    public String Direction { get; set; }

    /// <summary>地址或标签</summary>
    public String Address { get; set; }

    /// <summary>是否健康</summary>
    public Boolean Healthy { get; set; }
}

/// <summary>变更记录</summary>
public class ChangeItem
{
    /// <summary>应用/对象</summary>
    public String Target { get; set; }

    /// <summary>动作</summary>
    public String Action { get; set; }

    /// <summary>是否成功</summary>
    public Boolean Success { get; set; }

    /// <summary>发生时间</summary>
    public DateTime Time { get; set; }

    /// <summary>补充说明</summary>
    public String Remark { get; set; }

    /// <summary>时间文本</summary>
    public String TimeText => Time.Year > 2000 ? Time.ToString("MM-dd HH:mm") : "—";
}

/// <summary>节点信息</summary>
public class NodeInfo
{
    /// <summary>节点编号</summary>
    public Int32 Id { get; set; }

    /// <summary>节点名称</summary>
    public String Name { get; set; }

    /// <summary>IP 地址</summary>
    public String Ip { get; set; }

    /// <summary>操作系统</summary>
    public String OS { get; set; }

    /// <summary>系统版本</summary>
    public String OSVersion { get; set; }

    /// <summary>架构</summary>
    public String Arch { get; set; }

    /// <summary>机房/位置</summary>
    public String Location { get; set; }

    /// <summary>版本</summary>
    public String Version { get; set; }

    /// <summary>在线时长。秒</summary>
    public Int32 Uptime { get; set; }

    /// <summary>是否在线</summary>
    public Boolean Online { get; set; }

    /// <summary>最后活跃时间</summary>
    public DateTime LastActive { get; set; }

    /// <summary>项目编号</summary>
    public Int32 ProjectId { get; set; }

    /// <summary>项目名称</summary>
    public String ProjectName { get; set; }

    /// <summary>在线时长文本</summary>
    public String UptimeText => DashboardMath.Duration(Uptime);
}

/// <summary>节点资源时序</summary>
public class NodeSeriesModel
{
    /// <summary>横轴标签</summary>
    public String[] Categories { get; set; } = [];

    /// <summary>CPU 使用率</summary>
    public Double[] Cpu { get; set; } = [];

    /// <summary>内存使用率</summary>
    public Double[] Memory { get; set; } = [];

    /// <summary>磁盘使用率</summary>
    public Double[] Disk { get; set; } = [];
}

/// <summary>健康雷达</summary>
public class RadarModel
{
    /// <summary>维度名称</summary>
    public List<String> Indicators { get; set; } = [];

    /// <summary>各维度得分</summary>
    public Double[] Values { get; set; } = [];

    /// <summary>综合得分</summary>
    public Int32 Score { get; set; }

    /// <summary>等级文本</summary>
    public String Level { get; set; }

    /// <summary>等级配色。ok/warn/bad</summary>
    public String Tone { get; set; }
}

/// <summary>网络质量</summary>
public class NetworkModel
{
    /// <summary>内网评分</summary>
    public Double Intranet { get; set; }

    /// <summary>外网评分</summary>
    public Double Internet { get; set; }

    /// <summary>网关延迟。毫秒</summary>
    public Int32 GatewayLatency { get; set; }

    /// <summary>网关丢包率</summary>
    public Double GatewayLoss { get; set; }

    /// <summary>DNS 延迟。毫秒</summary>
    public Int32 DnsLatency { get; set; }

    /// <summary>DNS 丢包率</summary>
    public Double DnsLoss { get; set; }

    /// <summary>服务端延迟。毫秒</summary>
    public Int32 ServerLatency { get; set; }

    /// <summary>服务端丢包率</summary>
    public Double ServerLoss { get; set; }
}

/// <summary>磁盘分区</summary>
public class DiskItem
{
    /// <summary>分区盘符/挂载点</summary>
    public String Name { get; set; }

    /// <summary>文件系统</summary>
    public String Format { get; set; }

    /// <summary>已用大小。GB</summary>
    public Double Used { get; set; }

    /// <summary>总大小。GB</summary>
    public Double Total { get; set; }

    /// <summary>使用率百分比</summary>
    public Double Usage { get; set; }

    /// <summary>预计写满天数。0 表示未知</summary>
    public Int32 DaysToFull { get; set; }
}

/// <summary>进程守护项</summary>
public class ProcessItem
{
    /// <summary>进程名</summary>
    public String Name { get; set; }

    /// <summary>是否在运行</summary>
    public Boolean Running { get; set; }
}

/// <summary>节点已部署应用</summary>
public class DeployNodeItem
{
    /// <summary>部署集名称</summary>
    public String DeployName { get; set; }

    /// <summary>节点编号</summary>
    public Int32 NodeId { get; set; }

    /// <summary>版本</summary>
    public String Version { get; set; }

    /// <summary>端口</summary>
    public Int32 Port { get; set; }

    /// <summary>进程编号</summary>
    public Int32 ProcessId { get; set; }

    /// <summary>启动时间</summary>
    public DateTime StartTime { get; set; }

    /// <summary>最后活跃时间</summary>
    public DateTime LastActive { get; set; }

    /// <summary>是否启用</summary>
    public Boolean Enable { get; set; }

    /// <summary>运行状态。running/stopped</summary>
    public String Status => ProcessId > 0 ? "running" : "stopped";

    /// <summary>部署集看板地址</summary>
    public String Url => $"/Deployment/AppDeployNode?nodeId={NodeId}";
}

/// <summary>操作历史</summary>
public class HistoryItem
{
    /// <summary>动作</summary>
    public String Action { get; set; }

    /// <summary>是否成功</summary>
    public Boolean Success { get; set; }

    /// <summary>版本</summary>
    public String Version { get; set; }

    /// <summary>发生时间</summary>
    public DateTime Time { get; set; }

    /// <summary>备注</summary>
    public String Remark { get; set; }

    /// <summary>时间文本</summary>
    public String TimeText => Time.Year > 2000 ? Time.ToString("MM-dd HH:mm") : "—";
}

/// <summary>选项行。用于顶栏与列表</summary>
public class OptionRow
{
    /// <summary>编号</summary>
    public Int32 Id { get; set; }

    /// <summary>名称</summary>
    public String Name { get; set; }

    /// <summary>补充说明</summary>
    public String Text { get; set; }
}
