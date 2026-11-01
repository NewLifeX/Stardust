using NewLife;

namespace Stardust.Web.Models.Dashboard;

/// <summary>看板下钻链接。集中生成列表与详情地址，避免视图散落硬编码路径</summary>
/// <remarks>
/// 所有链接均落到魔方现有列表页或详情页，查询参数只携带实体过滤条件，不携带看板时间范围，
/// 避免分享链接时时间窗口漂移。列表页自身会使用默认时间范围。
/// </remarks>
public static class DashboardLinks
{
    #region 看板
    /// <summary>项目看板</summary>
    /// <param name="id">项目编号</param>
    /// <returns>看板地址</returns>
    public static String Project(Int32 id) => $"/Dashboard/Project/{id}";

    /// <summary>应用看板</summary>
    /// <param name="id">应用编号</param>
    /// <returns>看板地址</returns>
    public static String App(Int32 id) => $"/Dashboard/App/{id}";

    /// <summary>节点看板</summary>
    /// <param name="id">节点编号</param>
    /// <returns>看板地址</returns>
    public static String Node(Int32 id) => $"/Dashboard/Node/{id}";

    /// <summary>项目选择页</summary>
    /// <returns>列表地址</returns>
    public static String Projects() => "/Dashboard/Projects";

    /// <summary>应用选择页</summary>
    /// <returns>列表地址</returns>
    public static String Apps() => "/Dashboard/Apps";

    /// <summary>节点选择页</summary>
    /// <returns>列表地址</returns>
    public static String Nodes() => "/Dashboard/Nodes";
    #endregion

    #region 平台列表
    /// <summary>应用列表</summary>
    /// <param name="projectId">项目编号，0 表示不过滤</param>
    /// <returns>列表地址</returns>
    public static String AppList(Int32 projectId = 0) => projectId > 0 ? $"/Registry/App?projectId={projectId}" : "/Registry/App";

    /// <summary>节点列表</summary>
    /// <param name="projectId">项目编号，0 表示不过滤</param>
    /// <returns>列表地址</returns>
    public static String NodeList(Int32 projectId = 0) => projectId > 0 ? $"/Nodes/Node?projectId={projectId}" : "/Nodes/Node";

    /// <summary>在线实例列表</summary>
    /// <param name="appId">应用编号，0 表示不过滤</param>
    /// <param name="projectId">项目编号，0 表示不过滤</param>
    /// <param name="nodeId">节点编号，0 表示不过滤</param>
    /// <returns>列表地址</returns>
    public static String AppOnline(Int32 appId = 0, Int32 projectId = 0, Int32 nodeId = 0)
    {
        var query = new List<String>();
        if (appId > 0) query.Add("appId=" + appId);
        if (projectId > 0) query.Add("projectId=" + projectId);
        if (nodeId > 0) query.Add("nodeId=" + nodeId);

        return query.Count == 0 ? "/Registry/AppOnline" : "/Registry/AppOnline?" + String.Join("&", query);
    }

    /// <summary>在线节点列表</summary>
    /// <param name="nodeId">节点编号，0 表示不过滤</param>
    /// <param name="projectId">项目编号，0 表示不过滤</param>
    /// <returns>列表地址</returns>
    public static String NodeOnline(Int32 nodeId = 0, Int32 projectId = 0)
    {
        var query = new List<String>();
        if (nodeId > 0) query.Add("nodeId=" + nodeId);
        if (projectId > 0) query.Add("projectId=" + projectId);

        return query.Count == 0 ? "/Nodes/NodeOnline" : "/Nodes/NodeOnline?" + String.Join("&", query);
    }

    /// <summary>节点性能数据</summary>
    /// <param name="nodeId">节点编号</param>
    /// <returns>列表地址</returns>
    public static String NodeData(Int32 nodeId) => nodeId > 0 ? $"/Nodes/NodeData?nodeId={nodeId}" : "/Nodes/NodeData";

    /// <summary>节点操作历史</summary>
    /// <param name="nodeId">节点编号</param>
    /// <returns>列表地址</returns>
    public static String NodeHistory(Int32 nodeId) => nodeId > 0 ? $"/Nodes/NodeHistory?nodeId={nodeId}" : "/Nodes/NodeHistory";

    /// <summary>节点详情</summary>
    /// <param name="nodeId">节点编号</param>
    /// <returns>详情地址</returns>
    public static String NodeDetail(Int32 nodeId) => $"/Nodes/Node/Detail?id={nodeId}";

    /// <summary>部署集列表</summary>
    /// <param name="projectId">项目编号，0 表示不过滤</param>
    /// <returns>列表地址</returns>
    public static String DeployList(Int32 projectId = 0) => projectId > 0 ? $"/Deployment/AppDeploy?projectId={projectId}" : "/Deployment/AppDeploy";

    /// <summary>部署节点列表</summary>
    /// <param name="nodeId">节点编号，0 表示不过滤</param>
    /// <param name="appId">应用编号，0 表示不过滤</param>
    /// <returns>列表地址</returns>
    public static String DeployNodes(Int32 nodeId = 0, Int32 appId = 0)
    {
        var query = new List<String>();
        if (nodeId > 0) query.Add("nodeId=" + nodeId);
        if (appId > 0) query.Add("appId=" + appId);

        return query.Count == 0 ? "/Deployment/AppDeployNode" : "/Deployment/AppDeployNode?" + String.Join("&", query);
    }
    #endregion

    #region 监控列表
    /// <summary>应用分钟统计</summary>
    /// <param name="appId">应用编号，0 表示不过滤</param>
    /// <returns>列表地址</returns>
    public static String AppMinuteStat(Int32 appId = 0) => appId > 0 ? $"/Monitors/AppMinuteStat?appId={appId}" : "/Monitors/AppMinuteStat";

    /// <summary>跟踪小时统计</summary>
    /// <param name="appId">应用编号，0 表示不过滤</param>
    /// <param name="name">端点名称，为空表示不过滤</param>
    /// <returns>列表地址</returns>
    public static String TraceHourStat(Int32 appId = 0, String name = null)
    {
        var query = new List<String>();
        if (appId > 0) query.Add("appId=" + appId);
        if (!name.IsNullOrEmpty()) query.Add("name=" + Uri.EscapeDataString(name));

        return query.Count == 0 ? "/Monitors/TraceHourStat" : "/Monitors/TraceHourStat?" + String.Join("&", query);
    }

    /// <summary>跟踪数据列表</summary>
    /// <param name="appId">应用编号，0 表示不过滤</param>
    /// <param name="name">端点名称，为空表示不过滤</param>
    /// <param name="errorsOnly">是否只看错误</param>
    /// <returns>列表地址</returns>
    public static String TraceData(Int32 appId = 0, String name = null, Boolean errorsOnly = false)
    {
        var query = new List<String>();
        if (appId > 0) query.Add("appId=" + appId);
        if (!name.IsNullOrEmpty()) query.Add("name=" + Uri.EscapeDataString(name));
        if (errorsOnly) query.Add("minError=1");

        return query.Count == 0 ? "/Monitors/TraceData" : "/Monitors/TraceData?" + String.Join("&", query);
    }

    /// <summary>应用性能采样</summary>
    /// <param name="appId">应用编号</param>
    /// <returns>列表地址</returns>
    public static String AppMeter(Int32 appId) => appId > 0 ? $"/Registry/AppMeter?appId={appId}" : "/Registry/AppMeter";

    /// <summary>注册服务</summary>
    /// <param name="appId">应用编号</param>
    /// <returns>列表地址</returns>
    public static String AppService(Int32 appId) => appId > 0 ? $"/Registry/AppService?appId={appId}" : "/Registry/AppService";

    /// <summary>应用变更历史</summary>
    /// <param name="appId">应用编号</param>
    /// <returns>列表地址</returns>
    public static String AppHistory(Int32 appId) => appId > 0 ? $"/Registry/AppHistory?appId={appId}" : "/Registry/AppHistory";

    /// <summary>告警记录列表</summary>
    /// <param name="activeOnly">是否只看报警中</param>
    /// <returns>列表地址</returns>
    public static String AlarmList(Boolean activeOnly = false) => activeOnly ? "/Monitors/AlarmRecord?status=1" : "/Monitors/AlarmRecord";

    /// <summary>告警详情</summary>
    /// <param name="id">告警编号</param>
    /// <returns>详情地址</returns>
    public static String AlarmDetail(Int64 id) => $"/Monitors/AlarmRecord/Detail?id={id}";
    #endregion
}
