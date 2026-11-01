using System.Collections.Concurrent;
using NewLife;
using NewLife.Data;
using Stardust.Data;
using Stardust.Data.Deployment;
using Stardust.Data.Monitors;
using Stardust.Data.Nodes;
using Stardust.Data.Platform;
using Stardust.Web.Models.Dashboard;
using XCode;
using XCode.Membership;

namespace Stardust.Web.Services;

/// <summary>看板数据服务。集中装配系统/项目/应用/节点/大屏看板所需数据</summary>
/// <remarks>
/// 统计类查询走数据库分组聚合（Biz 中的 SearchGroupXxx），避免把明细行拉进内存；
/// 秒级刷新的页面用 10 秒本地缓存吸收重复请求。
/// </remarks>
public class DashboardService
{
    #region 范围
    /// <summary>解析当前用户的可见范围</summary>
    /// <param name="userId">用户编号</param>
    /// <returns>可见范围</returns>
    public ScopeInfo ResolveScope(Int32 userId)
    {
        var projects = GalaxyProject.FindAll();
        if (userId <= 0) return DashboardScope.Resolve(projects, false, []);

        var user = XCode.Membership.User.FindByID(userId);
        var isAdmin = user != null && IsSystemRole(user);
        var members = user == null ? [] : ProjectUser.FindAllByUserId(userId).Where(e => e.Enable).Select(e => e.ProjectId).ToList();

        return DashboardScope.Resolve(projects, isAdmin, members);
    }

    private static Boolean IsSystemRole(XCode.Membership.User user)
    {
        var roleIds = new List<Int32>();
        if (user.RoleID > 0) roleIds.Add(user.RoleID);
        if (!user.RoleIds.IsNullOrEmpty())
        {
            foreach (var item in user.RoleIds.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                if (Int32.TryParse(item.Trim(), out var id) && id > 0) roleIds.Add(id);
            }
        }

        if (roleIds.Count == 0) return false;

        var roles = Role.FindAll();
        return roles.Any(e => roleIds.Contains(e.ID) && e.IsSystem);
    }

    /// <summary>项目是否在可见范围内。管理员可看到未分配项目的资源</summary>
    /// <param name="scope">可见范围</param>
    /// <param name="projectId">项目编号</param>
    /// <returns>是否可见</returns>
    public static Boolean IsVisible(ScopeInfo scope, Int32 projectId)
    {
        if (scope == null || scope.IsNone) return false;
        if (scope.IsAdmin) return true;

        return scope.ProjectIds.Contains(projectId);
    }
    #endregion

    #region 系统级看板
    /// <summary>构建系统级看板</summary>
    /// <param name="scope">可见范围</param>
    /// <param name="range">时间范围</param>
    /// <returns>看板模型</returns>
    public SystemDashboardModel BuildSystem(ScopeInfo scope, String range)
    {
        var key = $"system:{scope.Mode}:{scope.ProjectIds.Join(",")}:{range}";
        return Cache(key, 10, () => BuildSystemCore(scope, range));
    }

    private SystemDashboardModel BuildSystemCore(ScopeInfo scope, String range)
    {
        var model = new SystemDashboardModel { Page = "system", Scope = scope, Range = range };
        if (scope.IsNone) return model;

        var now = DateTime.Now;
        var projects = LoadProjects(scope);
        var apps = LoadApps(scope);
        var appIds = apps.Select(e => e.Id).ToArray();
        var onlineApps = LoadOnlineApps(scope);
        var (onlineNodes, nodeMap) = LoadOnlineNodes(scope);

        model.TotalApps = apps.Count;
        model.TotalNodes = (Int32)projects.Sum(e => Node.FindCount(Node._.ProjectId == e.Id & Node._.Enable == true));

        // 今日统计。优先分钟统计（当天实时），无数据时回落昨日天统计
        var today = LoadAppAggregate(appIds, DateTime.Today, now, now.AddDays(-1));
        var yesterday = LoadAppAggregate(appIds, DateTime.Today.AddDays(-1), DateTime.Today, null, true);
        var days = Math.Max((now - DateTime.Today).TotalSeconds, 60);

        var onlineAppIds = onlineApps.Select(e => e.AppId).Where(e => e > 0).Distinct().ToHashSet();
        var onlineNodeIds = onlineNodes.Keys.ToHashSet();

        // 节点告警判定
        var warnNodes = onlineNodes.Values.Where(e => IsResourceAlarm(e, nodeMap.TryGetValue(e.NodeID, out var n) ? n : null)).ToList();
        var totalNodes = model.TotalNodes;
        var online = onlineNodeIds.Count;
        var offline = Math.Max(totalNodes - online, 0);

        model.Health = new ClusterHealth
        {
            Online = online,
            Warn = warnNodes.Count,
            Offline = offline,
            Rate = Math.Round(DashboardMath.Percent(online, totalNodes), 1),
        };
        model.Health.Items.AddRange(BuildHealthItems(warnNodes, online, nodeMap, scope));

        // 趋势
        model.Trend = BuildTrend(appIds, range);
        model.CurrentQps = RecentRate(model.Trend.Primary);

        // KPI
        var total = today.Values.Sum(e => e.Total);
        var errors = today.Values.Sum(e => e.Errors);
        var cost = total > 0 ? (Int32)(today.Values.Sum(e => e.TotalCost) / total) : 0;
        var errorRate = Math.Round(DashboardMath.ErrorRate(errors, total), 2);
        var alarms = LoadAlarms(scope, null, 0, 200).Count;

        model.Kpis.Add(new KpiModel
        {
            Key = "projects",
            Label = "项目（可见范围）",
            Value = scope.VisibleProjects.ToString("n0"),
            Unit = "个",
            Note = $"共 {scope.TotalProjects} 个项目 · 负责人 {projects.Count(e => e.ManagerId > 0)} 位",
            Tone = "brand",
            Url = DashboardLinks.Projects(),
        });
        model.Kpis.Add(new KpiModel
        {
            Key = "apps",
            Label = "在线应用",
            Value = onlineAppIds.Count.ToString("n0"),
            Unit = $"/ {apps.Count:n0}",
            Note = $"在线率 {DashboardMath.Percent(onlineAppIds.Count, apps.Count):n1}%",
            Delta = CompareDelta(onlineAppIds.Count, null),
            DeltaLevel = "flat",
            Tone = "online",
            Spark = DashboardMath.BucketAverage(model.Trend.Primary, 16),
            Url = DashboardLinks.AppList(),
        });
        model.Kpis.Add(new KpiModel
        {
            Key = "nodes",
            Label = "节点在线",
            Value = online.ToString("n0"),
            Unit = $"/ {totalNodes:n0}",
            Note = $"告警 {warnNodes.Count:n0} · 离线 {offline:n0}",
            Tone = offline > 0 ? "alert" : "online",
            Spark = DashboardMath.BucketAverage(model.Trend.Primary.Select(e => e * 0.8).ToArray(), 16),
            SparkColor = "var(--sd-color-alert)",
            Url = DashboardLinks.Nodes(),
        });
        model.Kpis.Add(new KpiModel
        {
            Key = "requests",
            Label = "今日请求量",
            Value = DashboardMath.Abbreviate(total),
            Unit = "",
            Delta = CompareDelta(total, yesterday.Values.Sum(e => e.Total)),
            DeltaLevel = total >= yesterday.Values.Sum(e => e.Total) ? "good" : "bad",
            Note = "较昨日",
            Tone = "brand",
            Spark = DashboardMath.BucketAverage(model.Trend.Primary, 16),
            Url = DashboardLinks.AppMinuteStat(),
        });
        model.Kpis.Add(new KpiModel
        {
            Key = "cost",
            Label = "平均耗时",
            Value = cost.ToString("n0"),
            Unit = "ms",
            Note = $"最大 {today.Values.Sum(e => e.MaxCost):n0} ms",
            Tone = "accent",
            Url = DashboardLinks.AppMinuteStat(),
        });
        model.Kpis.Add(new KpiModel
        {
            Key = "errorRate",
            Label = "错误率",
            Value = errorRate.ToString("n2"),
            Unit = "%",
            Delta = $"活跃告警 {alarms:n0} 条",
            DeltaLevel = alarms > 0 ? "warn" : "flat",
            Note = $"今日错误 {errors:n0} 次",
            Tone = "offline",
            Spark = DashboardMath.BucketAverage(model.Trend.ErrorRate, 16),
            SparkColor = "var(--sd-color-offline)",
            Url = DashboardLinks.AlarmList(true),
        });

        // 项目健康矩阵
        foreach (var project in projects)
        {
            var prjApps = apps.Where(e => e.ProjectId == project.Id).ToList();
            var prjAppIds = prjApps.Select(e => e.Id).ToHashSet();
            var prjOnline = onlineAppIds.Count(e => prjAppIds.Contains(e));
            var prjNodes = (Int32)Node.FindCount(Node._.ProjectId == project.Id & Node._.Enable == true);
            var prjOnlineNodes = nodeMap.Values.Count(e => e.ProjectId == project.Id && onlineNodeIds.Contains(e.ID));
            Int64 pTotal = 0;
            Int64 pErrors = 0;
            foreach (var kv in today)
            {
                if (!prjAppIds.Contains(kv.Key)) continue;
                pTotal += kv.Value.Total;
                pErrors += kv.Value.Errors;
            }
            var prjErrorRate = Math.Round(DashboardMath.ErrorRate(pErrors, pTotal), 2);
            var prjAlarms = LoadAlarms(scope, null, project.Id, 200).Count;

            model.Projects.Add(new ProjectRow
            {
                Id = project.Id,
                Name = project.Name,
                Apps = prjApps.Count,
                AppsOnline = prjOnline,
                Nodes = prjNodes,
                NodesOnline = prjOnlineNodes,
                ErrorRate = prjErrorRate,
                Alarms = prjAlarms,
                Health = DashboardMath.Health(prjApps.Count, prjOnline, prjNodes, prjOnlineNodes, prjErrorRate, prjAlarms),
            });
        }
        model.Projects = model.Projects.OrderByDescending(e => e.Health).ThenBy(e => e.Id).ToList();

        // Top 应用
        var topApps = new List<TopAppItem>();
        foreach (var app in apps)
        {
            if (!today.TryGetValue(app.Id, out var agg) || agg.Total <= 0) continue;

            topApps.Add(new TopAppItem
            {
                Id = app.Id,
                ProjectId = app.ProjectId,
                Name = app.DisplayName.IsNullOrEmpty() ? app.Name : app.DisplayName,
                Qps = Math.Round(agg.Total / days, 2),
                ErrorRate = Math.Round(DashboardMath.ErrorRate(agg.Errors, agg.Total), 2),
                Cost = (Int32)(agg.TotalCost / agg.Total),
            });
        }
        topApps = topApps.OrderByDescending(e => e.Qps).ToList();
        model.TopApps = topApps.Take(8).ToList();

        // 拓扑：项目 → 应用
        model.Topology = BuildProjectTopology(model.Projects, topApps);

        // 资源水位
        var (regions, dimension) = BuildRegions(onlineNodes, nodeMap, projects);
        model.Regions = regions;
        model.RegionDimension = dimension;

        // 实时事件
        model.Events = LoadEvents(scope, appIds, nodeMap.Keys.ToArray(), 8);

        return model;
    }

    private static (Int64, Int64) Aggregate((Int64, Int64) sum, KeyValuePair<Int32, AppAggregate> item) =>
        (sum.Item1 + item.Value.Total, sum.Item2 + item.Value.Errors);

    private List<EventItem> BuildHealthItems(IList<NodeOnline> warnNodes, Int32 online, IDictionary<Int32, Node> nodeMap, ScopeInfo scope)
    {
        var list = new List<EventItem>();

        foreach (var item in warnNodes.OrderByDescending(e => e.CpuRate).Take(3))
        {
            nodeMap.TryGetValue(item.NodeID, out var node);
            var name = node?.Name ?? item.Name;
            var reason = item.CpuRate >= 80 ? $"CPU {item.CpuRate:n0}%" : MemoryRate(item) >= 80 ? $"内存 {MemoryRate(item):n0}%" : $"磁盘 {DiskRate(item):n0}%";
            list.Add(new EventItem
            {
                Time = item.UpdateTime,
                Level = "warn",
                Title = $"{name} {reason}",
                Meta = $"{(node?.Location.IsNullOrEmpty() == false ? node.Location : node?.Project?.Name ?? "未分组")} · 资源超阈值",
                Url = $"/Dashboard/Node/{item.NodeID}",
            });
        }

        if (list.Count == 0)
        {
            list.Add(new EventItem
            {
                Time = DateTime.Now,
                Level = "ok",
                Title = "在线节点资源正常",
                Meta = $"在线 {online} 台 · 暂无资源越线",
                Url = "/Dashboard/Nodes",
            });
        }

        return list;
    }
    #endregion

    #region 项目看板
    /// <summary>构建项目看板</summary>
    /// <param name="scope">可见范围</param>
    /// <param name="projectId">项目编号</param>
    /// <param name="range">时间范围</param>
    /// <returns>看板模型</returns>
    public ProjectDashboardModel BuildProject(ScopeInfo scope, Int32 projectId, String range)
    {
        var key = $"project:{scope.Mode}:{scope.ProjectIds.Join(",")}:{projectId}:{range}";
        return Cache(key, 10, () => BuildProjectCore(scope, projectId, range));
    }

    private ProjectDashboardModel BuildProjectCore(ScopeInfo scope, Int32 projectId, String range)
    {
        var model = new ProjectDashboardModel { Page = "project", Scope = scope, Range = range, Id = projectId };
        if (scope.IsNone) return model;

        var project = GalaxyProject.FindById(projectId);
        if (project == null || !IsVisible(scope, project.Id)) return model;

        model.Project = new ProjectInfo
        {
            Id = project.Id,
            Name = project.Name,
            Manager = project.ManagerName,
            Members = ProjectUser.FindAll(ProjectUser._.ProjectId == projectId).Count(e => e.Enable),
            CreateTime = project.CreateTime,
            Enable = project.Enable,
            Remark = project.Remark,
            DataScope = project.IsGlobal ? "全局项目 · 允许其它项目应用选用本项目的节点" : "项目内可见",
        };

        var now = DateTime.Now;
        var apps = LoadApps(scope).Where(e => e.ProjectId == projectId).ToList();
        var appIds = apps.Select(e => e.Id).ToArray();
        var onlineApps = LoadOnlineApps(scope).Where(e => e.ProjectId == projectId).ToList();
        var onlineNodeIds = onlineApps.Select(e => e.AppId).Distinct().Count();
        var (allOnlineNodes, nodeMap) = LoadOnlineNodes(scope);
        var onlineNodes = allOnlineNodes.Values.Where(e => nodeMap.TryGetValue(e.NodeID, out var n) && n.ProjectId == projectId).ToList();
        var today = LoadAppAggregate(appIds, DateTime.Today, now, now.AddDays(-1));
        var days = Math.Max((now - DateTime.Today).TotalSeconds, 60);

        var totalNodes = (Int32)Node.FindCount(Node._.ProjectId == projectId & Node._.Enable == true);
        var total = today.Values.Sum(e => e.Total);
        var errors = today.Values.Sum(e => e.Errors);
        var cost = total > 0 ? (Int32)(today.Values.Sum(e => e.TotalCost) / total) : 0;
        var alarms = LoadAlarms(scope, null, projectId, 200);

        model.Kpis.Add(new KpiModel { Key = "apps", Label = "在线应用", Value = apps.Count(e => onlineApps.Any(o => o.AppId == e.Id)).ToString("n0"), Unit = $"/ {apps.Count:n0}", Note = $"实例 {onlineApps.Count:n0} 个", Tone = "online", Url = DashboardLinks.AppList(projectId) });
        model.Kpis.Add(new KpiModel { Key = "nodes", Label = "节点在线", Value = onlineNodes.Count.ToString("n0"), Unit = $"/ {totalNodes:n0}", Note = $"离线 {Math.Max(totalNodes - onlineNodes.Count, 0):n0} 台", Tone = "alert", Url = DashboardLinks.NodeList(projectId) });
        model.Kpis.Add(new KpiModel { Key = "instances", Label = "服务实例", Value = onlineApps.Count.ToString("n0"), Unit = "个", Note = $"应用 {apps.Count:n0} 个", Tone = "brand", Url = DashboardLinks.AppOnline(projectId: projectId) });
        model.Kpis.Add(new KpiModel { Key = "requests", Label = "今日请求量", Value = DashboardMath.Abbreviate(total), Unit = "", Note = $"QPS {total / days:n1}", Tone = "brand", Url = DashboardLinks.AppMinuteStat() });
        model.Kpis.Add(new KpiModel { Key = "cost", Label = "平均耗时", Value = cost.ToString("n0"), Unit = "ms", Note = $"最大 {today.Values.Sum(e => e.MaxCost):n0} ms", Tone = "accent", Url = DashboardLinks.AppMinuteStat() });
        model.Kpis.Add(new KpiModel { Key = "alarms", Label = "活跃告警", Value = alarms.Count.ToString("n0"), Unit = "条", Note = $"错误率 {DashboardMath.ErrorRate(errors, total):n2}%", Tone = alarms.Count > 0 ? "offline" : "online", Url = DashboardLinks.AlarmList(true) });

        // 应用矩阵
        foreach (var app in apps)
        {
            today.TryGetValue(app.Id, out var agg);
            var instances = onlineApps.Where(e => e.AppId == app.Id).ToList();
            model.Apps.Add(new AppCardModel
            {
                Id = app.Id,
                Name = app.Name,
                DisplayName = app.DisplayName.IsNullOrEmpty() ? app.Name : app.DisplayName,
                Status = instances.Count == 0 ? "offline" : (agg?.ErrorRate ?? 0) >= 1 ? "warn" : "online",
                Instances = instances.Count,
                Qps = agg == null ? 0 : Math.Round(agg.Total / days, 2),
                ErrorRate = agg == null ? 0 : Math.Round(DashboardMath.ErrorRate(agg.Errors, agg.Total), 2),
                Cost = agg == null || agg.Total <= 0 ? 0 : (Int32)(agg.TotalCost / agg.Total),
                Version = instances.FirstOrDefault()?.Version ?? app.Version,
                Spark = BuildAppSpark(app.Id, appIds),
            });
        }
        model.Apps = model.Apps.OrderByDescending(e => e.ErrorRate).ThenByDescending(e => e.Qps).ToList();

        // 节点资源
        foreach (var item in onlineNodes.OrderByDescending(e => e.CpuRate).Take(12))
        {
            nodeMap.TryGetValue(item.NodeID, out var node);
            model.Nodes.Add(new NodeResourceModel
            {
                Id = item.NodeID,
                Name = node?.Name ?? item.Name,
                Ip = node?.IP ?? item.IP,
                Location = node?.Location ?? item.Location,
                Online = true,
                Cpu = Math.Round(item.CpuRate, 1),
                Memory = Math.Round(MemoryRate(item), 1),
                Disk = Math.Round(DiskRate(item), 1),
            });
        }

        // 拓扑：项目 → 应用 → 节点
        model.Topology = BuildProjectDetailTopology(project, model.Apps, model.Nodes);

        // 最近部署
        var deploys = AppDeploy.FindAll(AppDeploy._.ProjectId == projectId);
        var deployIds = deploys.Select(e => e.Id).ToHashSet();
        var histories = AppDeployHistory.FindAll(AppDeployHistory._.CreateTime >= DateTime.Now.AddDays(-7), AppDeployHistory._.CreateTime.Desc(), null, 0, 50)
            .Where(e => deployIds.Contains(e.DeployId))
            .Take(8);
        foreach (var item in histories)
        {
            var deploy = deploys.FirstOrDefault(e => e.Id == item.DeployId);
            model.Deploys.Add(new DeployItem
            {
                App = deploy?.Name ?? item.DeployId + "",
                Version = deploy?.Version,
                Action = item.Action,
                Success = item.Success,
                Time = item.CreateTime,
            });
        }

        model.Alarms = alarms.Take(8).ToList();

        return model;
    }

    /// <summary>项目选择页</summary>
    /// <param name="scope">可见范围</param>
    /// <returns>选择页模型</returns>
    public PickerPageModel BuildProjectPicker(ScopeInfo scope)
    {
        var model = new PickerPageModel { Page = "projects", Scope = scope, Title = "项目看板", Subtitle = "选择项目查看应用与节点健康" };
        if (scope.IsNone) return model;

        var system = BuildSystem(scope, "24h");
        model.Projects = system.Projects;

        return model;
    }
    #endregion

    #region 应用看板
    /// <summary>构建应用看板</summary>
    /// <param name="scope">可见范围</param>
    /// <param name="appId">应用编号</param>
    /// <param name="range">时间范围</param>
    /// <returns>看板模型</returns>
    public AppDashboardModel BuildApp(ScopeInfo scope, Int32 appId, String range)
    {
        var key = $"app:{scope.Mode}:{scope.ProjectIds.Join(",")}:{appId}:{range}";
        return Cache(key, 10, () => BuildAppCore(scope, appId, range));
    }

    private AppDashboardModel BuildAppCore(ScopeInfo scope, Int32 appId, String range)
    {
        var model = new AppDashboardModel { Page = "app", Scope = scope, Range = range, Id = appId };
        if (scope.IsNone) return model;

        var app = App.FindById(appId);
        if (app == null || !IsVisible(scope, app.ProjectId)) return model;

        var now = DateTime.Now;
        var online = LoadOnlineApps(scope).Where(e => e.AppId == appId).ToList();
        var today = LoadAppAggregate([appId], DateTime.Today, now, now.AddDays(-1));
        today.TryGetValue(appId, out var agg);
        var days = Math.Max((now - DateTime.Today).TotalSeconds, 60);

        model.Info = new AppInfo
        {
            Id = app.Id,
            Name = app.Name,
            DisplayName = app.DisplayName.IsNullOrEmpty() ? app.Name : app.DisplayName,
            ProjectId = app.ProjectId,
            ProjectName = app.Project?.Name,
            Category = app.Category,
            Version = app.Version,
            Compile = app.Compile,
            Enable = app.Enable,
            Online = online.Count > 0,
            Instances = online.Count,
        };

        model.Trend = BuildAppTrend(appId, range);

        var total = agg?.Total ?? 0;
        var errors = agg?.Errors ?? 0;
        var cost = agg == null || agg.Total <= 0 ? 0 : (Int32)(agg.TotalCost / agg.Total);
        var services = AppService.FindAllByAppId(appId).Where(e => e.Enable).ToList();

        model.Kpis.Add(new KpiModel { Key = "qps", Label = range.EqualIgnoreCase("7d") ? "日均 QPS" : "当前 QPS", Value = RecentRate(model.Trend.Primary, 1).ToString("n1"), Unit = "", Note = $"今日 {DashboardMath.Abbreviate(total)} 次", Tone = "brand", Spark = DashboardMath.BucketAverage(model.Trend.Primary, 16), Url = DashboardLinks.AppMinuteStat(appId) });
        model.Kpis.Add(new KpiModel { Key = "cost", Label = "平均耗时", Value = cost.ToString("n0"), Unit = "ms", Note = $"最大 {agg?.MaxCost ?? 0:n0} ms", Tone = "accent", Url = DashboardLinks.AppMinuteStat(appId) });
        model.Kpis.Add(new KpiModel { Key = "maxCost", Label = "最大耗时", Value = (agg?.MaxCost ?? 0).ToString("n0"), Unit = "ms", Note = "当日峰值", Tone = "accent", Url = DashboardLinks.AppMinuteStat(appId) });
        model.Kpis.Add(new KpiModel { Key = "errorRate", Label = "错误率", Value = Math.Round(DashboardMath.ErrorRate(errors, total), 2).ToString("n2"), Unit = "%", Note = $"错误 {errors:n0} 次", Tone = "offline", Spark = DashboardMath.BucketAverage(model.Trend.ErrorRate, 16), SparkColor = "var(--sd-color-offline)", Url = DashboardLinks.AlarmList(true) });
        model.Kpis.Add(new KpiModel { Key = "instances", Label = "在线实例", Value = online.Count.ToString("n0"), Unit = "", Note = online.Count > 0 ? $"版本 {online.FirstOrDefault()?.Version}" : "暂无在线实例", Tone = online.Count > 0 ? "online" : "offline", Url = DashboardLinks.AppOnline(appId) });
        model.Kpis.Add(new KpiModel { Key = "services", Label = "注册服务", Value = services.Count.ToString("n0"), Unit = "个", Note = $"健康 {services.Count(e => e.Healthy):n0} 个", Tone = "brand", Url = DashboardLinks.AppService(appId) });

        // 实例健康
        var nodeIds = online.Select(e => e.NodeId).Where(e => e > 0).Distinct().ToArray();
        var nodes = nodeIds.Length > 0 ? Node.FindAll(Node._.ID.In(nodeIds)).ToDictionary(e => e.ID, e => e) : [];
        model.Instances = BuildInstanceHealth(appId, online, nodes);
        // 端点统计
        var endpoints = BuildEndpoints(appId, range == "7d" ? now.AddDays(-7) : now.AddHours(-24));
        model.Endpoints = endpoints.OrderByDescending(e => e.Cost).Take(8).ToList();
        model.ErrorEndpoints = endpoints.Where(e => e.Errors > 0).OrderByDescending(e => e.Errors).Take(6).ToList();
        model.ErrorTotal = endpoints.Sum(e => e.Errors);

        // 运行时性能
        model.Meter = BuildMeter(appId);

        // 依赖服务
        foreach (var item in services)
        {
            model.Dependencies.Add(new DependencyItem
            {
                Service = item.ServiceName.IsNullOrEmpty() ? item.ServiceId + "" : item.ServiceName,
                Direction = "provider",
                Address = item.Address,
                Healthy = item.Healthy,
            });
        }
        var consumes = AppConsume.FindAll(AppConsume._.AppId == appId);
        foreach (var item in consumes)
        {
            model.Dependencies.Add(new DependencyItem
            {
                Service = item.ServiceName.IsNullOrEmpty() ? item.ServiceId + "" : item.ServiceName,
                Direction = "consumer",
                Address = item.Address,
                Healthy = item.Enable,
            });
        }

        // 最近变更：只看发布与生命周期动作，过滤心跳、WebSocket、鉴权等运行日志；同一动作只留最新一条
        var changes = AppHistory.FindAll(AppHistory._.AppId == appId, AppHistory._.CreateTime.Desc(), null, 0, 60)
            .Where(e => _changeKeywords.Any(k => (e.Action ?? "").Contains(k)))
            .GroupBy(e => e.Action ?? "")
            .Select(g => g.First())
            .Take(8)
            .ToList();
        if (changes.Count == 0) changes = [.. AppHistory.FindAll(AppHistory._.AppId == appId, AppHistory._.CreateTime.Desc(), null, 0, 8)];
        foreach (var item in changes)
        {
            model.Changes.Add(new ChangeItem
            {
                Target = app.Name,
                Action = item.Action,
                Success = item.Success,
                Time = item.CreateTime,
                Remark = item.Remark,
            });
        }

        model.Alarms = LoadAlarms(scope, app.Name, app.ProjectId, 8);

        // 趋势填充 KPI
        return model;
    }

    /// <summary>应用选择页</summary>
    /// <param name="scope">可见范围</param>
    /// <returns>选择页模型</returns>
    public PickerPageModel BuildAppPicker(ScopeInfo scope)
    {
        var model = new PickerPageModel { Page = "apps", Scope = scope, Title = "应用看板", Subtitle = "选择应用查看在线状态、错误与慢端点" };
        if (scope.IsNone) return model;

        var apps = LoadApps(scope);
        var onlineApps = LoadOnlineApps(scope).Where(e => e.AppId > 0).GroupBy(e => e.AppId).ToDictionary(e => e.Key, e => e.ToList());
        var today = LoadAppAggregate(apps.Select(e => e.Id).ToArray(), DateTime.Today, DateTime.Now, DateTime.Now.AddDays(-1));
        var days = Math.Max((DateTime.Now - DateTime.Today).TotalSeconds, 60);

        foreach (var app in apps)
        {
            today.TryGetValue(app.Id, out var agg);
            onlineApps.TryGetValue(app.Id, out var sessions);
            model.Apps.Add(new AppCardModel
            {
                Id = app.Id,
                Name = app.Name,
                DisplayName = app.DisplayName.IsNullOrEmpty() ? app.Name : app.DisplayName,
                Status = sessions == null || sessions.Count == 0 ? "offline" : "online",
                Instances = sessions?.Count ?? 0,
                Qps = agg == null ? 0 : Math.Round(agg.Total / days, 2),
                ErrorRate = agg == null ? 0 : Math.Round(DashboardMath.ErrorRate(agg.Errors, agg.Total), 2),
                Cost = agg == null || agg.Total <= 0 ? 0 : (Int32)(agg.TotalCost / agg.Total),
                Version = sessions?.FirstOrDefault()?.Version ?? app.Version,
            });
        }
        model.Apps = model.Apps.OrderByDescending(e => e.Status == "online").ThenByDescending(e => e.ErrorRate).Take(60).ToList();

        return model;
    }
    #endregion

    #region 节点看板
    /// <summary>构建节点看板</summary>
    /// <param name="scope">可见范围</param>
    /// <param name="nodeId">节点编号</param>
    /// <param name="range">时间范围</param>
    /// <returns>看板模型</returns>
    public NodeDashboardModel BuildNode(ScopeInfo scope, Int32 nodeId, String range)
    {
        var key = $"node:{scope.Mode}:{scope.ProjectIds.Join(",")}:{nodeId}:{range}";
        return Cache(key, 10, () => BuildNodeCore(scope, nodeId, range));
    }

    private NodeDashboardModel BuildNodeCore(ScopeInfo scope, Int32 nodeId, String range)
    {
        var model = new NodeDashboardModel { Page = "node", Scope = scope, Range = range, Id = nodeId };
        if (scope.IsNone) return model;

        var node = Node.FindByID(nodeId);
        if (node == null || !IsVisible(scope, node.ProjectId)) return model;

        var online = NodeOnline.FindAllByNodeId(nodeId).OrderByDescending(e => e.UpdateTime).FirstOrDefault();
        var memoryTotal = node.Memory;
        var diskTotal = node.TotalSize;

        model.Info = new NodeInfo
        {
            Id = node.ID,
            Name = node.Name,
            Ip = node.IP,
            OS = node.OS,
            OSVersion = node.OSVersion,
            Arch = node.Architecture,
            Location = node.Location,
            Version = node.Version,
            Uptime = online?.Uptime ?? 0,
            Online = online != null,
            LastActive = online?.UpdateTime ?? node.LastActive,
            ProjectId = node.ProjectId,
            ProjectName = node.Project?.Name,
        };
        model.LastReport = model.Info.LastActive;
        if (online == null)
        {
            var offlineMinutes = (Int32)Math.Max((DateTime.Now - model.Info.LastActive).TotalMinutes, 0);
            model.OfflineText = offlineMinutes > 0 ? DashboardMath.Duration(offlineMinutes * 60) : "未知";
        }

        var cpu = online?.CpuRate ?? 0;
        var memory = online == null ? 0 : MemoryRate(online);
        var disk = online == null ? 0 : DiskRate(online);
        var load = online?.SystemLoad ?? 0;
        var temperature = online?.Temperature ?? 0;
        var uplink = online?.UplinkSpeed ?? 0;
        var downlink = online?.DownlinkSpeed ?? 0;
        model.Cpu = Math.Round(cpu, 1);
        model.Memory = Math.Round(memory, 1);
        model.Disk = Math.Round(disk, 1);

        model.Kpis.Add(new KpiModel { Key = "cpu", Label = "CPU 使用率", Value = cpu.ToString("n0"), Unit = "%", Note = $"{node.Cpu} 核", Tone = DashboardMath.Level(cpu) == "ok" ? "online" : "alert", Spark = [], Url = DashboardLinks.NodeData(node.ID) });
        model.Kpis.Add(new KpiModel { Key = "memory", Label = "内存使用率", Value = memory.ToString("n0"), Unit = "%", Note = $"{node.Memory / 1024d:n1} GB 总量", Tone = "accent", Url = DashboardLinks.NodeData(node.ID) });
        model.Kpis.Add(new KpiModel { Key = "disk", Label = "磁盘使用率", Value = disk.ToString("n0"), Unit = "%", Note = diskTotal > 0 ? $"{diskTotal / 1024d:n1} GB 总量" : "—", Tone = DashboardMath.Level(disk) == "ok" ? "brand" : "alert", Url = DashboardLinks.NodeData(node.ID) });
        model.Kpis.Add(new KpiModel { Key = "load", Label = "系统负载", Value = load.ToString("n2"), Unit = "", Note = $"{node.Cpu} 核 · {load / Math.Max(node.Cpu, 1):n2}/核", Tone = "brand", Url = DashboardLinks.NodeOnline(node.ID) });
        model.Kpis.Add(new KpiModel { Key = "temperature", Label = "温度", Value = temperature > 0 ? temperature.ToString("n0") : "—", Unit = temperature > 0 ? "℃" : "", Note = temperature >= 70 ? "温度偏高" : "正常", Tone = temperature >= 70 ? "offline" : "online", Url = DashboardLinks.NodeOnline(node.ID) });
        model.Kpis.Add(new KpiModel { Key = "network", Label = "网络吞吐", Value = DashboardMath.Abbreviate((uplink + downlink) / 1024d / 1024 * 8), Unit = "Mbps", Note = $"↑ {DashboardMath.Abbreviate(uplink / 1024d / 1024 * 8)} · ↓ {DashboardMath.Abbreviate(downlink / 1024d / 1024 * 8)}", Tone = "online", Url = DashboardLinks.NodeOnline(node.ID) });

        // 时序
        model.Series = BuildNodeSeries(node, range);

        // 健康雷达
        var uptimeDays = online == null ? 0 : online.Uptime / 86400d;
        var (indicators, values, score) = DashboardMath.Radar(cpu, memory, disk, online?.IntranetScore ?? 0, temperature, uptimeDays);
        model.Radar = new RadarModel
        {
            Indicators = indicators.ToList(),
            Values = values,
            Score = score,
            Level = DashboardMath.HealthLevel(score),
            Tone = DashboardMath.HealthTone(score),
        };

        // 网络质量
        if (online != null)
        {
            model.Network = new NetworkModel
            {
                Intranet = Math.Round(online.IntranetScore, 1),
                Internet = Math.Round(online.InternetScore, 1),
                GatewayLatency = online.GatewayLatency,
                GatewayLoss = Math.Round(online.GatewayLossRate, 2),
                DnsLatency = online.DnsLatency,
                DnsLoss = Math.Round(online.DnsLossRate, 2),
                ServerLatency = online.ServerLatency,
                ServerLoss = Math.Round(online.ServerLossRate, 2),
            };
        }

        // 磁盘分区
        model.Disks = DashboardMath.ParseDrives(!node.DriveInfo.IsNullOrEmpty() ? node.DriveInfo : online?.DriveInfo).ToList();

        // 进程守护
        model.Processes = DashboardMath.BuildGuard(node.AlarmProcesses, online?.Processes).ToList();
        model.ProcessCount = online?.ProcessCount ?? 0;
        model.TcpConnections = online?.TcpConnections ?? 0;

        // 已部署应用
        foreach (var item in AppDeployNode.FindAllByNodeId(nodeId))
        {
            model.Deploys.Add(new DeployNodeItem
            {
                DeployName = item.DeployName,
                NodeId = item.NodeId,
                Version = item.Version,
                Port = item.Port,
                ProcessId = item.ProcessId,
                StartTime = item.StartTime,
                LastActive = item.LastActive,
                Enable = item.Enable,
            });
        }

        // 操作历史
        foreach (var item in NodeHistory.FindAll(NodeHistory._.NodeID == nodeId, NodeHistory._.CreateTime.Desc(), null, 0, 10))
        {
            model.History.Add(new HistoryItem
            {
                Action = item.Action,
                Success = item.Success,
                Version = item.Version,
                Time = item.CreateTime,
                Remark = item.Remark,
            });
        }

        model.Alarms = LoadAlarms(scope, node.Name, node.ProjectId, 8);

        return model;
    }

    /// <summary>节点选择页</summary>
    /// <param name="scope">可见范围</param>
    /// <returns>选择页模型</returns>
    public PickerPageModel BuildNodePicker(ScopeInfo scope)
    {
        var model = new PickerPageModel { Page = "nodes", Scope = scope, Title = "节点看板", Subtitle = "选择节点查看资源、网络与部署" };
        if (scope.IsNone) return model;

        var onlineNodes = LoadOnlineNodes(scope);
        foreach (var item in onlineNodes.Online.Values.OrderByDescending(e => e.CpuRate).Take(60))
        {
            if (!onlineNodes.Nodes.TryGetValue(item.NodeID, out var node)) continue;

            model.Nodes.Add(new NodeResourceModel
            {
                Id = node.ID,
                Name = node.Name,
                Ip = node.IP.IsNullOrEmpty() ? item.IP : node.IP,
                Location = node.Location,
                Online = true,
                Cpu = Math.Round(item.CpuRate, 1),
                Memory = Math.Round(MemoryRate(item), 1),
                Disk = Math.Round(DiskRate(item), 1),
            });
        }

        return model;
    }
    #endregion

    #region 大屏
    /// <summary>构建大屏看板</summary>
    /// <param name="scope">可见范围</param>
    /// <param name="range">时间范围</param>
    /// <returns>大屏模型</returns>
    public WallboardModel BuildWallboard(ScopeInfo scope, String range)
    {
        var key = $"wall:{scope.Mode}:{scope.ProjectIds.Join(",")}:{range}";
        return Cache(key, 10, () => BuildWallboardCore(scope, range));
    }

    private WallboardModel BuildWallboardCore(ScopeInfo scope, String range)
    {
        var model = new WallboardModel { Page = "wallboard", Scope = scope, Range = range };
        if (scope.IsNone) return model;

        var system = BuildSystem(scope, range);
        var now = DateTime.Now;
        var apps = LoadApps(scope);
        var appIds = apps.Select(e => e.Id).ToArray();
        var today = LoadAppAggregate(appIds, DateTime.Today, now, now.AddDays(-1));
        var yesterday = LoadAppAggregate(appIds, DateTime.Today.AddDays(-1), DateTime.Today, null, true);
        var onlineApps = LoadOnlineApps(scope).Where(e => e.AppId > 0).Select(e => e.AppId).Distinct().Count();

        var total = today.Values.Sum(e => e.Total);
        var errors = today.Values.Sum(e => e.Errors);
        var yTotal = yesterday.Values.Sum(e => e.Total);
        var yErrors = yesterday.Values.Sum(e => e.Errors);
        var errorRate = Math.Round(DashboardMath.ErrorRate(errors, total), 2);
        var yErrorRate = Math.Round(DashboardMath.ErrorRate(yErrors, yTotal), 2);

        model.NodesOnline = system.Health.Online;
        model.NodesTotal = system.Health.Online + system.Health.Offline;
        model.NodesWarn = system.Health.Warn;
        model.NodesOffline = system.Health.Offline;
        model.NodeOnlineRate = Math.Round(DashboardMath.Percent(model.NodesOnline, model.NodesTotal), 1);
        model.NodeRateHistory = BuildNodeRateHistory(model.NodesTotal, now);
        model.TodayRequests = total;
        model.ErrorRate = errorRate;
        model.ErrorRateDelta = Math.Round(errorRate - yErrorRate, 2);
        model.CurrentQps = system.CurrentQps;
        model.PeakQps = system.Trend.Primary.Length > 0 ? system.Trend.Primary.Max() : 0;
        model.ValleyQps = system.Trend.Primary.Length > 0 ? system.Trend.Primary.Min() : 0;
        model.AvgCost = total > 0 ? (Int32)(today.Values.Sum(e => e.TotalCost) / total) : 0;
        model.AppsOnline = onlineApps;
        model.AppsTotal = apps.Count;
        model.TodayDeploys = (Int32)AppDeployHistory.FindCount(AppDeployHistory._.CreateTime >= DateTime.Today);
        model.Trend = system.Trend;
        model.Projects = system.Projects;
        model.Events = system.Events;
        model.TopApps = system.TopApps;
        model.Regions = system.Regions;
        model.RegionDimension = system.RegionDimension;

        var alarms = LoadAlarms(scope, null, 0, 200);
        model.Alarms = alarms.Count;
        model.AlarmAlarming = alarms.Count(e => !e.Recovered);
        model.AlarmRecovered = alarms.Count(e => e.Recovered);
        var page = new PageParameter();
        AlarmHistory.Search(-1, DateTime.Today, now, null, page);
        model.AlarmToday = (Int32)page.TotalCount;

        foreach (var item in AlarmRecord.SearchGroupCategory(DateTime.Now.AddDays(-7), DateTime.Now))
        {
            if (item.Category.IsNullOrEmpty()) continue;
            model.AlarmByCategory.Add(new NameCount { Name = item.Category, Count = (Int32)item.Times });
        }
        model.AlarmByCategory = model.AlarmByCategory.OrderByDescending(e => e.Count).Take(6).ToList();

        return model;
    }
    #endregion

    #region 辅助
    /// <summary>应用变更动作关键字。用于从运行日志中挑出发布与生命周期事件</summary>
    private static readonly String[] _changeKeywords = ["上线", "下线", "注册", "重启", "升级", "部署", "安装", "卸载", "版本", "停止", "启动", "启用", "禁用", "超时"];

    private sealed record CacheItem(Object Value, DateTime Expire);

    private readonly ConcurrentDictionary<String, CacheItem> _cache = new();

    /// <summary>清空看板缓存。报警确认或静默后立即生效</summary>
    public void ClearCache() => _cache.Clear();

    private T Cache<T>(String key, Int32 seconds, Func<T> factory) where T : class
    {
        if (_cache.TryGetValue(key, out var item) && item.Expire > DateTime.Now && item.Value is T value) return value;

        var data = factory();
        if (data != null) _cache[key] = new CacheItem(data, DateTime.Now.AddSeconds(seconds));

        return data;
    }

    private static IList<GalaxyProject> LoadProjects(ScopeInfo scope)
    {
        var list = GalaxyProject.FindAll().Where(e => e.Enable).ToList();
        return scope.IsAdmin ? list.OrderBy(e => e.Sort).ThenBy(e => e.Id).ToList() : list.Where(e => scope.ProjectIds.Contains(e.Id)).OrderBy(e => e.Sort).ThenBy(e => e.Id).ToList();
    }

    private static List<App> LoadApps(ScopeInfo scope) => App.FindAll()
        .Where(e => e.Enable && IsVisible(scope, e.ProjectId))
        .ToList();

    private static List<AppOnline> LoadOnlineApps(ScopeInfo scope)
    {
        var list = AppOnline.FindAll();
        return list.Where(e => e.AppId > 0).Where(e => scope.IsAdmin || scope.ProjectIds.Contains(e.ProjectId)).ToList();
    }

    private static (Dictionary<Int32, NodeOnline> Online, Dictionary<Int32, Node> Nodes) LoadOnlineNodes(ScopeInfo scope)
    {
        var list = NodeOnline.FindAll();
        var ids = list.Select(e => e.NodeID).Where(e => e > 0).Distinct().ToArray();
        var nodes = ids.Length > 0 ? Node.FindAll(Node._.ID.In(ids)) : [];
        var nodeMap = nodes.ToDictionary(e => e.ID, e => e);

        var online = list.Where(e => nodeMap.ContainsKey(e.NodeID))
            .Where(e => scope.IsAdmin || scope.ProjectIds.Contains(nodeMap[e.NodeID].ProjectId))
            .GroupBy(e => e.NodeID)
            .Select(g => g.OrderByDescending(e => e.UpdateTime).First())
            .ToDictionary(e => e.NodeID, e => e);

        return (online, nodeMap);
    }

    private sealed class AppAggregate
    {
        public Int64 Total { get; set; }
        public Int64 Errors { get; set; }
        public Int64 TotalCost { get; set; }
        public Int32 MaxCost { get; set; }
        public Double ErrorRate => DashboardMath.ErrorRate(Errors, Total);
    }

    /// <summary>按应用汇总指定时间段的统计。优先分钟统计，无数据时回落天统计</summary>
    private static Dictionary<Int32, AppAggregate> LoadAppAggregate(Int32[] appIds, DateTime start, DateTime end, DateTime? fallbackDate, Boolean dayOnly = false)
    {
        var dic = new Dictionary<Int32, AppAggregate>();
        if (appIds.Length == 0) return dic;

        if (!dayOnly)
        {
            foreach (var item in AppMinuteStat.SearchGroupApp(start, end, appIds))
            {
                if (!dic.TryGetValue(item.AppId, out var agg)) dic[item.AppId] = agg = new AppAggregate();
                agg.Total += item.Total;
                agg.Errors += item.Errors;
                agg.TotalCost += item.TotalCost;
                if (item.MaxCost > agg.MaxCost) agg.MaxCost = item.MaxCost;
            }
        }

        // 分钟统计为空时回落天统计，保证刚过零点或未上报时仍有数据
        if (dic.Count == 0 && fallbackDate != null)
        {
            var date = fallbackDate.Value.Date;
            foreach (var item in AppDayStat.SearchGroupApp(date, date, appIds))
            {
                if (!dic.TryGetValue(item.AppId, out var agg)) dic[item.AppId] = agg = new AppAggregate();
                agg.Total += item.Total;
                agg.Errors += item.Errors;
                agg.TotalCost += item.TotalCost;
                if (item.MaxCost > agg.MaxCost) agg.MaxCost = item.MaxCost;
            }
        }

        return dic;
    }

    private static TrendModel BuildTrend(Int32[] appIds, String range)
    {
        var model = new TrendModel();
        var now = DateTime.Now;

        if (range.EqualIgnoreCase("7d"))
        {
            var start = DateTime.Today.AddDays(-6);
            var dic = new Dictionary<DateTime, (Int64 Total, Int64 Errors)>();
            foreach (var item in AppDayStat.SearchGroupDate(start, DateTime.Today, appIds))
            {
                var date = item.StatDate.Date;
                dic.TryGetValue(date, out var cur);
                dic[date] = (cur.Total + item.Total, cur.Errors + item.Errors);
            }

            for (var i = 0; i < 7; i++)
            {
                var date = start.AddDays(i);
                dic.TryGetValue(date, out var cur);
                model.Categories = [.. model.Categories, date.ToString("MM-dd")];
                model.Primary = [.. model.Primary, Math.Round(cur.Total / 86400d, 2)];
                model.ErrorRate = [.. model.ErrorRate, Math.Round(DashboardMath.ErrorRate(cur.Errors, cur.Total), 2)];
            }

            return model;
        }

        var isHour = range.EqualIgnoreCase("1h");
        var bucket = isHour ? 1 : 5;
        var from = now.AddHours(isHour ? -1 : -24);
        var startTime = new DateTime(from.Year, from.Month, from.Day, from.Hour, from.Minute / bucket * bucket, 0);

        var dic2 = new Dictionary<DateTime, (Int64 Total, Int64 Errors)>();
        foreach (var item in AppMinuteStat.SearchGroupTime(startTime, now, appIds))
        {
            var time = new DateTime(item.StatTime.Year, item.StatTime.Month, item.StatTime.Day, item.StatTime.Hour, item.StatTime.Minute / bucket * bucket, 0);
            dic2.TryGetValue(time, out var cur);
            dic2[time] = (cur.Total + item.Total, cur.Errors + item.Errors);
        }

        // 补齐完整时间轴，缺失区间以 0 呈现，避免横轴跳点；当前桶尚未走完，不纳入以免尾部跌到 0
        for (var time = startTime; time < now; time = time.AddMinutes(bucket))
        {
            dic2.TryGetValue(time, out var cur);
            model.Categories = [.. model.Categories, time.ToString("HH:mm")];
            model.Primary = [.. model.Primary, Math.Round(cur.Total / (bucket * 60d), 2)];
            model.ErrorRate = [.. model.ErrorRate, Math.Round(DashboardMath.ErrorRate(cur.Errors, cur.Total), 2)];
        }

        return model;
    }

    private static TrendModel BuildAppTrend(Int32 appId, String range) => BuildTrend([appId], range);

    private static Double[] BuildAppSpark(Int32 appId, Int32[] appIds)
    {
        var start = DateTime.Today.AddDays(-6);
        var rows = AppDayStat.SearchGroupDate(start, DateTime.Today, [appId]);
        if (rows.Count == 0) return [];

        return rows.OrderBy(e => e.StatDate).Select(e => Math.Round(e.Total / 86400d, 2)).ToArray();
    }

    private static InstanceHealth BuildInstanceHealth(Int32 appId, IList<AppOnline> online, IDictionary<Int32, Node> nodes)
    {
        var model = new InstanceHealth
        {
            Online = online.Count,
            Total = Math.Max(online.Count, 1),
        };
        if (online.Count == 0) return model;

        model.AvgUptime = Math.Round(online.Average(e => Math.Max((DateTime.Now - (e.StartTime.Year > 2000 ? e.StartTime : e.CreateTime)).TotalHours, 0)), 1);
        model.Versions = online.GroupBy(e => e.Version ?? "")
            .Select(g => new NameCount { Name = g.Key.IsNullOrEmpty() ? "未知版本" : g.Key, Count = g.Count() })
            .OrderByDescending(e => e.Count)
            .ToList();

        var meters = AppMeter.FindAll(AppMeter._.AppId == appId, AppMeter._.Time.Desc(), null, 0, 200)
            .Where(e => !e.ClientId.IsNullOrEmpty())
            .GroupBy(e => e.ClientId)
            .ToDictionary(g => g.Key, g => g.First());

        foreach (var item in online)
        {
            meters.TryGetValue(item.Client, out var meter);
            nodes.TryGetValue(item.NodeId, out var node);
            model.Rows.Add(new InstanceRow
            {
                Client = item.Client,
                NodeName = node?.Name ?? item.Name,
                NodeId = item.NodeId,
                Version = item.Version,
                Ip = item.IP,
                Uptime = Math.Round(Math.Max((DateTime.Now - (item.StartTime.Year > 2000 ? item.StartTime : item.CreateTime)).TotalHours, 0), 1),
                Cpu = Math.Round(meter?.CpuUsage ?? 0, 1),
                Memory = meter?.Memory ?? 0,
                Threads = meter?.Threads ?? 0,
                Connections = meter?.Connections ?? 0,
            });
        }

        return model;
    }

    private static List<EndpointItem> BuildEndpoints(Int32 appId, DateTime start)
    {
        var rows = TraceHourStat.SearchGroupName(appId, start, DateTime.Now);
        var list = new List<EndpointItem>();
        foreach (var item in rows)
        {
            if (item.Name.IsNullOrEmpty()) continue;

            list.Add(new EndpointItem
            {
                Name = item.Name,
                Total = item.Total,
                Errors = item.Errors,
                ErrorRate = Math.Round(DashboardMath.ErrorRate(item.Errors, item.Total), 2),
                Cost = item.Total > 0 ? (Int32)(item.TotalCost / item.Total) : 0,
                MaxCost = item.MaxCost,
            });
        }

        return list;
    }

    private static MeterModel BuildMeter(Int32 appId)
    {
        var model = new MeterModel();
        var list = AppMeter.FindAll(AppMeter._.AppId == appId, AppMeter._.Time.Desc(), null, 0, 50);
        if (list.Count == 0) return model;

        model.SampleTime = list[0].Time;
        model.Cpu = Math.Round(list.Average(e => e.CpuUsage), 1);
        model.Memory = (Int32)Math.Round(list.Average(e => e.Memory));
        model.Heap = (Int32)Math.Round(list.Average(e => e.HeapSize));
        model.Threads = (Int32)Math.Round(list.Average(e => e.Threads));
        model.Connections = (Int32)Math.Round(list.Average(e => e.Connections));
        model.GcCount = (Int32)Math.Round(list.Average(e => e.GCCount));

        return model;
    }

    private static NodeSeriesModel BuildNodeSeries(Node node, String range)
    {
        var model = new NodeSeriesModel();
        var now = DateTime.Now;
        DateTime start;
        Int32 buckets;
        if (range.EqualIgnoreCase("1h"))
        {
            start = now.AddHours(-1);
            buckets = 60;
        }
        else if (range.EqualIgnoreCase("7d"))
        {
            start = now.AddDays(-7);
            buckets = 168;
        }
        else
        {
            start = now.AddHours(-24);
            buckets = 144;
        }

        var rows = NodeData.FindAll(NodeData._.NodeID == node.ID & NodeData._.CreateTime.Between(start, now), NodeData._.Id.Asc(), null, 0, 0);
        if (rows.Count == 0) return model;

        // 超过目标点数时按时间等分采样，取每个桶的平均值
        var size = Math.Max((Int32)Math.Ceiling(rows.Count * 1d / buckets), 1);
        var memoryTotal = Math.Max(node.Memory, 1);
        var diskTotal = node.TotalSize > 0 ? node.TotalSize : (Int32)(rows.FirstOrDefault(e => e.AvailableFreeSpace > 0)?.AvailableFreeSpace ?? 0);
        if (diskTotal <= 0) diskTotal = 1;

        for (var i = 0; i < rows.Count; i += size)
        {
            var end = Math.Min(i + size, rows.Count);
            Double cpu = 0, memoryUsed = 0, used = 0;
            for (var j = i; j < end; j++)
            {
                var item = rows[j];
                cpu += item.CpuRate;
                memoryUsed += Math.Max(memoryTotal - item.AvailableMemory, 0);
                used += Math.Max(diskTotal - item.AvailableFreeSpace, 0);
            }

            var count = end - i;
            model.Categories = [.. model.Categories, rows[i].CreateTime.ToString(buckets > 100 ? "MM-dd HH" : "HH:mm")];
            model.Cpu = [.. model.Cpu, Math.Round(cpu / count, 1)];
            model.Memory = [.. model.Memory, Math.Round(DashboardMath.Percent(memoryUsed / count, memoryTotal), 1)];
            model.Disk = [.. model.Disk, Math.Round(DashboardMath.Percent(used / count, diskTotal), 1)];
        }

        return model;
    }

    /// <summary>按最近 24 小时、每小时取样计算节点在线率曲线</summary>
    /// <param name="total">节点总数</param>
    /// <param name="now">当前时间</param>
    /// <returns>在线率序列</returns>
    private static List<Double> BuildNodeRateHistory(Int32 total, DateTime now)
    {
        var list = new List<Double>();
        if (total <= 0) return list;

        var rows = NodeOnline.FindAll();
        for (var i = 23; i >= 0; i--)
        {
            var time = now.AddHours(-i);
            var count = rows.Count(e =>
            {
                var start = e.LoginTime.Year > 2000 ? e.LoginTime : e.CreateTime;
                return start <= time;
            });
            list.Add(Math.Round(count * 100d / total, 1));
        }

        return list;
    }

    /// <summary>按最近的完整时段估算实时速率。跳过尚未走完的当前桶，避免尾桶偏小</summary>
    /// <param name="values">分桶速率序列</param>
    /// <param name="windows">参与平均的完整桶数</param>
    /// <returns>实时速率</returns>
    private static Double RecentRate(Double[] values, Int32 windows = 3)
    {
        if (values.Length == 0) return 0;

        var complete = Math.Min(values.Length - 1, windows);
        if (complete <= 0) return Math.Round(values[^1], 2);

        var sum = 0d;
        for (var i = values.Length - 1 - complete; i < values.Length - 1; i++) sum += values[i];

        return Math.Round(sum / complete, 2);
    }

    private static TopologyModel BuildProjectTopology(IList<ProjectRow> projects, IList<TopAppItem> apps)
    {
        var model = new TopologyModel { Categories = ["项目", "应用"] };
        var list = projects.OrderByDescending(e => e.Apps).Take(4).ToList();

        // 每个项目各取自己的 Top 应用，避免只显示全局 Top 导致大项目成为孤立节点
        var groups = list
            .Select(e => (Project: e, Children: apps.Where(a => a.ProjectId == e.Id).Take(3).ToList()))
            .ToList();

        var rows = groups.Sum(e => Math.Max(1, e.Children.Count));
        var step = rows > 1 ? 300d / (rows - 1) : 0;
        var index = 0;
        foreach (var (project, children) in groups)
        {
            var count = Math.Max(1, children.Count);
            var start = index;
            index += count;

            var y = 10 + step * (start + (count - 1) / 2d);
            model.Nodes.Add(new TopologyNode
            {
                Key = $"p{project.Id}",
                Name = project.Name,
                Category = 0,
                X = 90,
                Y = y,
                SymbolSize = 44,
                Value = project.Apps,
                Url = project.Url,
            });

            for (var j = 0; j < children.Count; j++)
            {
                var item = children[j];
                model.Nodes.Add(new TopologyNode
                {
                    Key = $"a{item.Id}",
                    Name = item.Name,
                    Category = 1,
                    X = 350,
                    Y = 10 + step * (start + j),
                    SymbolSize = (Int32)Math.Round(24 + Math.Min(item.Qps, 26)),
                    Value = item.Qps,
                    Url = item.Url,
                });
                model.Links.Add(new TopologyLink { Source = $"p{project.Id}", Target = $"a{item.Id}", Value = item.Qps });
            }
        }

        return model;
    }

    private static TopologyModel BuildProjectDetailTopology(GalaxyProject project, IList<AppCardModel> apps, IList<NodeResourceModel> nodes)
    {
        var model = new TopologyModel { Categories = ["项目", "应用", "节点"] };
        model.Nodes.Add(new TopologyNode { Key = "p", Name = project.Name, Category = 0, X = 80, Y = 140, SymbolSize = 52, Value = apps.Count, Url = $"/Dashboard/Project/{project.Id}" });

        var appList = apps.Take(8).ToList();
        for (var i = 0; i < appList.Count; i++)
        {
            var item = appList[i];
            var y = appList.Count == 1 ? 0.5 : i / (appList.Count - 1d);
            model.Nodes.Add(new TopologyNode
            {
                Key = $"a{item.Id}",
                Name = item.DisplayName,
                Category = 1,
                X = 320,
                Y = 24 + y * 250,
                SymbolSize = (Int32)Math.Round(26 + Math.Min(item.Qps, 24)),
                Value = item.Qps,
                Url = item.Url,
            });
            model.Links.Add(new TopologyLink { Source = "p", Target = $"a{item.Id}", Value = item.Instances });
        }

        var nodeList = nodes.Take(10).ToList();
        for (var i = 0; i < nodeList.Count; i++)
        {
            var item = nodeList[i];
            var y = nodeList.Count == 1 ? 0.5 : i / (nodeList.Count - 1d);
            model.Nodes.Add(new TopologyNode
            {
                Key = $"n{item.Id}",
                Name = item.Name,
                Category = 2,
                X = 560,
                Y = 14 + y * 260,
                SymbolSize = 24,
                Value = item.Cpu,
                Url = item.Url,
            });
            var app = appList.Count > 0 ? appList[i % appList.Count] : null;
            if (app != null) model.Links.Add(new TopologyLink { Source = $"a{app.Id}", Target = $"n{item.Id}", Value = 1 });
        }

        return model;
    }

    private static (List<RegionRow>, String) BuildRegions(IDictionary<Int32, NodeOnline> online, IDictionary<Int32, Node> nodeMap, IList<GalaxyProject> projects)
    {
        var list = online.Values.ToList();
        var hasLocation = list.Any(e => nodeMap.TryGetValue(e.NodeID, out var n) && !n.Location.IsNullOrEmpty());

        if (hasLocation)
        {
            var rows = list.GroupBy(e => nodeMap.TryGetValue(e.NodeID, out var n) && !n.Location.IsNullOrEmpty() ? n.Location : "未标注机房")
                .Select(g => new RegionRow
                {
                    Name = g.Key,
                    Nodes = g.Count(),
                    Cpu = Math.Round(g.Average(e => e.CpuRate), 1),
                    Memory = Math.Round(g.Average(MemoryRate), 1),
                    Disk = Math.Round(g.Average(DiskRate), 1),
                })
                .OrderByDescending(e => e.Nodes)
                .Take(8)
                .ToList();
            return (rows, "机房");
        }

        var rows2 = list.GroupBy(e => nodeMap.TryGetValue(e.NodeID, out var n) ? n.ProjectId : 0)
            .Select(g =>
            {
                var project = projects.FirstOrDefault(e => e.Id == g.Key);
                return new RegionRow
                {
                    Name = project?.Name ?? "未分配项目",
                    ProjectId = g.Key,
                    Nodes = g.Count(),
                    Cpu = Math.Round(g.Average(e => e.CpuRate), 1),
                    Memory = Math.Round(g.Average(MemoryRate), 1),
                    Disk = Math.Round(g.Average(DiskRate), 1),
                };
            })
            .OrderByDescending(e => e.Nodes)
            .Take(8)
            .ToList();

        return (rows2, "项目");
    }

    private static List<AlarmItem> LoadAlarms(ScopeInfo scope, String target, Int32 projectId, Int32 max)
    {
        if (scope.IsNone) return [];

        var records = AlarmRecord.FindAll(null, AlarmRecord._.StartTime.Desc(), null, 0, 200);
        if (records.Count == 0) return [];

        HashSet<Int32> groups = null;
        if (!scope.IsAdmin)
        {
            var ids = scope.ProjectIds;
            groups = ids.Length == 0 ? [] : AlarmGroup.FindAll(AlarmGroup._.ProjectId.In(ids)).Select(e => e.Id).ToHashSet();
        }
        else if (projectId > 0)
        {
            // 直接查库，避免告警组实体缓存尚未刷新导致误判
            groups = AlarmGroup.FindAll(AlarmGroup._.ProjectId == projectId).Select(e => e.Id).ToHashSet();
        }

        var list = new List<AlarmItem>();
        var nameCache = new Dictionary<String, Boolean>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in records)
        {
            if (!target.IsNullOrEmpty())
            {
                if (!item.Action.EqualIgnoreCase(target)) continue;
            }
            else if (groups != null && !groups.Contains(item.GroupId))
            {
                var action = item.Action ?? "";
                if (!nameCache.TryGetValue(action, out var matched))
                {
                    matched = IsVisibleName(scope, action, projectId);
                    nameCache[action] = matched;
                }
                if (!matched) continue;
            }

            list.Add(new AlarmItem
            {
                Id = item.Id,
                Target = item.Action,
                Category = item.Category,
                StartTime = item.StartTime,
                Duration = item.Duration,
                Times = item.Times,
                Recovered = item.Status == AlarmStatuses.Recovered,
                Content = item.Content,
                AckTime = item.AckTime,
                AckUser = item.AckUser,
                SilenceTime = item.SilenceTime,
            });
            if (list.Count >= max) break;
        }

        return list;
    }

    /// <summary>判断当前用户能否操作指定报警记录</summary>
    /// <param name="scope">可见范围</param>
    /// <param name="record">报警记录</param>
    /// <returns>是否允许确认或静默</returns>
    public Boolean CanOperateAlarm(ScopeInfo scope, AlarmRecord record)
    {
        if (scope == null || scope.IsNone || record == null) return false;
        if (scope.IsAdmin) return true;

        var projectId = ResolveAlarmProjectId(record);
        return projectId > 0 && scope.ProjectIds.Contains(projectId);
    }

    private static Int32 ResolveAlarmProjectId(AlarmRecord record)
    {
        if (record.GroupId > 0)
        {
            var group = AlarmGroup.FindById(record.GroupId);
            if (group != null && group.ProjectId > 0) return group.ProjectId;
        }

        var action = record.Action;
        if (action.IsNullOrEmpty()) return 0;

        // 埋点告警的对象形如 appName/itemName，取第一段用于定位应用
        var name = action.Split('/', '\\')[0];
        var app = App.FindByName(name);
        if (app != null) return app.ProjectId;

        var node = Node.FindByName(name);
        return node?.ProjectId ?? 0;
    }

    private static Boolean IsVisibleName(ScopeInfo scope, String action, Int32 projectId)
    {
        if (action.IsNullOrEmpty()) return false;

        // 指定项目时只认该项目下的应用与节点，避免管理员跨项目误命中
        Boolean InProject(Int32 id) => projectId > 0 ? id == projectId : scope.ProjectIds.Contains(id);

        var apps = App.FindAll();
        if (apps.Any(e => InProject(e.ProjectId) && e.Name.EqualIgnoreCase(action))) return true;

        var node = Node.Find(Node._.Name == action);
        return node != null && InProject(node.ProjectId);
    }

    private static List<EventItem> LoadEvents(ScopeInfo scope, Int32[] appIds, Int32[] nodeIds, Int32 max)
    {
        var list = new List<EventItem>();
        var appMap = App.FindAll().ToDictionary(e => e.Id, e => e.DisplayName.IsNullOrEmpty() ? e.Name : e.DisplayName);

        foreach (var item in AlarmRecord.FindAll(AlarmRecord._.StartTime >= DateTime.Now.AddDays(-1), AlarmRecord._.StartTime.Desc(), null, 0, 10))
        {
            var level = item.Status == AlarmStatuses.Alarming ? "bad" : "ok";
            list.Add(new EventItem
            {
                Time = item.StartTime,
                Level = level,
                Title = $"{item.Category} · {item.Action}",
                Meta = item.Content,
                Url = DashboardLinks.AlarmDetail(item.Id),
                AlarmId = item.Id,
                Acked = item.AckTime.Year > 2000,
                Silenced = item.SilenceTime > DateTime.Now,
                AckText = item.AckTime.Year > 2000 ? $"{item.AckUser} 已确认" : null,
                SilenceTime = item.SilenceTime,
                SilenceText = item.SilenceTime > DateTime.Now ? $"静默至 {item.SilenceTime:MM-dd HH:mm}" : null,
            });
        }

        foreach (var item in AppHistory.FindAll(AppHistory._.CreateTime >= DateTime.Now.AddDays(-1), AppHistory._.CreateTime.Desc(), null, 0, 20))
        {
            if (appIds.Length > 0 && !appIds.Contains(item.AppId)) continue;
            appMap.TryGetValue(item.AppId, out var appName);
            list.Add(new EventItem
            {
                Time = item.CreateTime,
                Level = item.Success ? "info" : "warn",
                Title = $"应用 {appName ?? item.AppId + ""} {item.Action}",
                Meta = $"版本 {item.Version} · {item.Creator}",
                Url = $"/Dashboard/App/{item.AppId}",
            });
        }

        foreach (var item in AppDeployHistory.FindAll(AppDeployHistory._.CreateTime >= DateTime.Now.AddDays(-1), AppDeployHistory._.CreateTime.Desc(), null, 0, 20))
        {
            list.Add(new EventItem
            {
                Time = item.CreateTime,
                Level = item.Success ? "ok" : "bad",
                Title = $"部署 {item.Action}",
                Meta = item.Remark,
                Url = "/Deployment/AppDeployHistory",
            });
        }

        if (nodeIds.Length > 0)
        {
            foreach (var item in NodeHistory.FindAll(NodeHistory._.CreateTime >= DateTime.Now.AddDays(-1), NodeHistory._.CreateTime.Desc(), null, 0, 20))
            {
                if (!nodeIds.Contains(item.NodeID)) continue;
                list.Add(new EventItem
                {
                    Time = item.CreateTime,
                    Level = item.Success ? "info" : "warn",
                    Title = $"节点 {item.Name} {item.Action}",
                    Meta = item.Remark,
                    Url = $"/Dashboard/Node/{item.NodeID}",
                });
            }
        }

        // 同一对象同一分钟的重复事件只保留一条，避免刷屏
        return list.OrderByDescending(e => e.Time)
            .GroupBy(e => e.Title + "|" + e.TimeText)
            .Select(g => g.First())
            .Take(max)
            .ToList();
    }

    /// <summary>内存使用率</summary>
    /// <param name="online">节点在线数据</param>
    /// <returns>百分比</returns>
    public static Double MemoryRate(NodeOnline online)
    {
        if (online == null || online.Memory <= 0) return 0;

        return DashboardMath.Percent(online.MemoryUsed, online.Memory);
    }

    /// <summary>磁盘使用率</summary>
    /// <param name="online">节点在线数据</param>
    /// <returns>百分比</returns>
    public static Double DiskRate(NodeOnline online)
    {
        if (online == null) return 0;

        var total = online.SpaceUsed + online.AvailableFreeSpace;
        return total <= 0 ? 0 : DashboardMath.Percent(online.SpaceUsed, total);
    }

    private static Boolean IsResourceAlarm(NodeOnline online, Node node)
    {
        var cpuLimit = node?.AlarmCpuRate > 0 ? node.AlarmCpuRate : 90;
        var memoryLimit = node?.AlarmMemoryRate > 0 ? node.AlarmMemoryRate : 90;
        var diskLimit = node?.AlarmDiskRate > 0 ? node.AlarmDiskRate : 90;

        return DashboardMath.IsResourceAlarm(online.CpuRate, MemoryRate(online), DiskRate(online), cpuLimit, memoryLimit, diskLimit);
    }

    private static String CompareDelta(Int64 current, Int64? previous)
    {
        if (previous == null || previous.Value <= 0) return null;

        var rate = (current - previous.Value) * 100d / previous.Value;
        return rate >= 0 ? $"▲ {Math.Abs(rate):n1}%" : $"▼ {Math.Abs(rate):n1}%";
    }
    #endregion
}
