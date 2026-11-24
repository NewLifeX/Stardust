using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using NewLife;
using NewLife.Log;
using Stardust.Data.Configs;
using Stardust.Data.Deployment;
using Stardust.Data.Gateway;
using Stardust.Data.Monitors;
using Stardust.Data.Nodes;
using XCode;
using XCode.Configuration;
using XCode.DataAccessLayer;

namespace Stardust.Data.Platform;

/// <summary>项目范围过滤模式</summary>
public enum ProjectScopeMode
{
    /// <summary>非星尘实体，不参与过滤</summary>
    None,

    /// <summary>直接带 ProjectId</summary>
    Project,

    /// <summary>项目自身，按 Id 过滤</summary>
    ProjectSelf,

    /// <summary>经 AppId 关联应用</summary>
    AppId,

    /// <summary>经 NodeId 关联节点</summary>
    NodeId,

    /// <summary>经其它父级外键关联</summary>
    Parent,

    /// <summary>仅管理员可见的全局资源</summary>
    AdminOnly,

    /// <summary>全局资源，不参与项目过滤</summary>
    Global,

    /// <summary>未分类。按失败关闭处理</summary>
    Unknown,
}

/// <summary>项目范围规则。负责实体分类、查询条件、读取过滤与写入校验</summary>
public static class ProjectScopeRules
{
    #region 分类
    private static readonly ConcurrentDictionary<Type, ProjectScopeMode> _modes = new();
    private static readonly ConcurrentDictionary<Type, Boolean> _warned = new();

    /// <summary>仅管理员可见的全局资源</summary>
    private static readonly Type[] _adminOnlyTypes = [];

    /// <summary>不参与项目过滤的全局资源</summary>
    private static readonly Type[] _globalTypes =
    [
        typeof(AppRule),
        typeof(TraceRule),
        typeof(NodeRule),
        typeof(NodeLocation),
        typeof(NodeVersion),
        typeof(NodeStat),
        typeof(ProductRelease),
        typeof(ProductPackage),
        typeof(DotNetPackage),
        typeof(Attachment),
        typeof(SslCertificate),
        typeof(DomainProvider),
    ];

    /// <summary>父级外键映射。按顺序匹配，先命中者生效</summary>
    private static readonly (String Field, Type Parent)[] _parentLinks =
    [
        ("ConfigId", typeof(AppConfig)),
        ("DeployId", typeof(AppDeploy)),
        ("PipelineId", typeof(AppPipeline)),
        ("RunId", typeof(AppPipelineRun)),
        ("GroupId", typeof(AlarmGroup)),
        ("ItemId", typeof(TraceItem)),
        ("TokenId", typeof(McpToken)),
        ("ClusterId", typeof(GatewayCluster)),
        ("MySqlId", typeof(MySqlNode)),
        ("RedisId", typeof(RedisNode)),
    ];

    /// <summary>获取实体过滤模式</summary>
    /// <param name="entityType">实体类型</param>
    /// <returns>过滤模式</returns>
    public static ProjectScopeMode GetMode(Type entityType)
    {
        if (entityType == null) return ProjectScopeMode.None;

        if (_modes.TryGetValue(entityType, out var mode)) return mode;

        mode = ResolveMode(entityType);
        _modes.TryAdd(entityType, mode);

        return mode;
    }

    private static ProjectScopeMode ResolveMode(Type type)
    {
        var ns = type.Namespace;
        if (ns == null || (ns != "Stardust.Data" && !ns.StartsWith("Stardust.Data."))) return ProjectScopeMode.None;

        if (type == typeof(GalaxyProject)) return ProjectScopeMode.ProjectSelf;
        if (_adminOnlyTypes.Contains(type)) return ProjectScopeMode.AdminOnly;
        if (_globalTypes.Contains(type)) return ProjectScopeMode.Global;

        var factory = type.AsFactory();
        if (factory == null) return ProjectScopeMode.Unknown;

        if (FindField(factory, "ProjectId") != null) return ProjectScopeMode.Project;
        if (FindField(factory, "AppId") != null) return ProjectScopeMode.AppId;
        if (FindField(factory, "NodeId") != null) return ProjectScopeMode.NodeId;
        if (FindParentLink(factory) != null) return ProjectScopeMode.Parent;

        return ProjectScopeMode.Unknown;
    }

    private static FieldItem FindField(IEntityFactory factory, String name)
    {
        var fields = factory?.Fields;
        if (fields == null) return null;

        foreach (var item in fields)
        {
            if (item.Name.EqualIgnoreCase(name)) return item;
        }

        return null;
    }

    private static (String Field, Type Parent)? FindParentLink(IEntityFactory factory)
    {
        foreach (var item in _parentLinks)
        {
            if (FindField(factory, item.Field) != null) return item;
        }

        return null;
    }

    private static void Warn(Type type)
    {
        if (_warned.TryAdd(type, true))
            XTrace.WriteLine("[ProjectScope] Unclassified entity type {0}; access denied by default.", type.FullName);
    }
    #endregion

    #region 查询过滤
    /// <summary>构造查询条件。返回 null 表示不过滤</summary>
    /// <param name="factory">实体工厂</param>
    /// <param name="scope">项目范围</param>
    /// <returns>查询条件</returns>
    public static Expression BuildQueryFilter(IEntityFactory factory, ProjectScope scope)
    {
        if (factory == null || scope == null) return null;
        if (scope.IsAdmin) return null;

        var mode = GetMode(factory.EntityType);
        switch (mode)
        {
            case ProjectScopeMode.Project:
                return BuildInProject(factory, "ProjectId", scope);

            case ProjectScopeMode.ProjectSelf:
                return BuildInProject(factory, factory.Unique?.Name ?? "Id", scope);

            case ProjectScopeMode.AppId:
                return BuildParentFilter(factory, "AppId", typeof(Stardust.Data.App), scope);

            case ProjectScopeMode.NodeId:
                return BuildParentFilter(factory, "NodeId", typeof(Node), scope);

            case ProjectScopeMode.Parent:
                var link = FindParentLink(factory);
                return link == null ? Deny() : BuildParentFilter(factory, link.Value.Field, link.Value.Parent, scope);

            case ProjectScopeMode.AdminOnly:
                return Deny();

            case ProjectScopeMode.Global:
            case ProjectScopeMode.None:
                return null;

            default:
                Warn(factory.EntityType);
                return Deny();
        }
    }

    private static Expression BuildInProject(IEntityFactory factory, String fieldName, ProjectScope scope)
    {
        if (scope.ProjectIds == null || scope.ProjectIds.Length == 0) return Deny();

        var field = FindField(factory, fieldName);
        return field?.In(scope.ProjectIds) ?? Deny();
    }

    private static Expression BuildParentFilter(IEntityFactory factory, String fieldName, Type parentType, ProjectScope scope)
    {
        var field = FindField(factory, fieldName);
        if (field == null) return Deny();

        var parentFactory = parentType.AsFactory();
        if (parentFactory == null) return Deny();

        // 父级本身是全局资源，子表不参与项目过滤
        if (GetMode(parentType) == ProjectScopeMode.Global || GetMode(parentType) == ProjectScopeMode.None) return null;

        if (IsSameConnection(factory, parentFactory))
        {
            var parentFilter = BuildQueryFilter(parentFactory, scope);
            if (parentFilter == null) return null;
            if (parentFilter.IsEmpty) return Deny();

            var db = parentFactory.Session?.Dal?.Db;
            var key = parentFactory.Unique?.Name ?? "Id";
            var table = parentFactory.Table?.TableName;
            if (table.IsNullOrEmpty()) return Deny();

            var sb = new SelectBuilder
            {
                Column = db != null ? db.FormatName(key) : key,
                Table = db != null ? db.FormatName(table) : table,
                Where = parentFilter.ToString(),
            };

            return field.In(sb);
        }

        // 跨库无法使用子查询，物化父级编号后走 IN 列表
        var ids = scope.GetParentIds(parentType, t => LoadParentIds(t, scope));

        return BuildInValues(field, ids);
    }

    private static Boolean IsSameConnection(IEntityFactory left, IEntityFactory right)
    {
        var lc = left?.ConnName;
        var rc = right?.ConnName;
        if (lc.IsNullOrEmpty() && rc.IsNullOrEmpty()) return true;

        return String.Equals(lc, rc, StringComparison.OrdinalIgnoreCase);
    }

    private static Expression BuildInValues(FieldItem field, Int32[] ids)
    {
        if (ids == null || ids.Length == 0) return Deny();

        // 参数化列表过大时改用整型拼接，避免触发数据库参数上限
        if (ids.Length <= 500) return field.In(ids);

        return field.In(ids.Join(","));
    }

    private static Int32[] LoadParentIds(Type parentType, ProjectScope scope)
    {
        var parentFactory = parentType.AsFactory();
        if (parentFactory == null) return [];

        var filter = BuildQueryFilter(parentFactory, scope);
        if (filter == null) return [];

        var list = parentFactory.FindAll(filter, null, null, 0, 0);
        if (list == null || list.Count == 0) return [];

        var key = parentFactory.Unique?.Name ?? "Id";
        var set = new HashSet<Int32>();
        foreach (var item in list)
        {
            if (item is EntityBase eb)
            {
                var id = eb[key].ToInt();
                if (id > 0) set.Add(id);
            }
        }

        var ids = set.ToArray();
        Array.Sort(ids);

        return ids;
    }

    private static Expression Deny() => new("1=0");
    #endregion

    #region 读取过滤
    /// <summary>过滤单个实体。不在当前项目范围时返回 null</summary>
    /// <remarks>实体缓存与单对象缓存查找不经过查询条件，Web 层用本方法补可见性校验。</remarks>
    /// <typeparam name="T">实体类型</typeparam>
    /// <param name="entity">实体</param>
    /// <returns>可见实体或 null</returns>
    public static T FilterVisible<T>(T entity) where T : class, IEntity
    {
        if (entity == null) return null;

        var scope = ProjectScopeContext.Current;
        if (scope == null || scope.IsAdmin) return entity;

        return CanAccess(entity, scope) ? entity : null;
    }

    /// <summary>过滤实体列表。不在当前项目范围时剔除</summary>
    /// <typeparam name="T">实体类型</typeparam>
    /// <param name="list">实体列表</param>
    /// <returns>过滤后的列表</returns>
    public static IList<T> FilterVisible<T>(IList<T> list) where T : class, IEntity
    {
        if (list == null || list.Count == 0) return list ?? [];

        var scope = ProjectScopeContext.Current;
        if (scope == null || scope.IsAdmin) return list;

        var result = new List<T>(list.Count);
        foreach (var item in list)
        {
            if (item != null && CanAccess(item, scope)) result.Add(item);
        }

        return result;
    }

    /// <summary>判断实体是否在项目范围内</summary>
    /// <param name="entity">实体</param>
    /// <param name="scope">项目范围</param>
    /// <returns>是否允许访问</returns>
    public static Boolean CanAccess(IEntity entity, ProjectScope scope)
    {
        if (entity == null) return true;
        if (scope == null || scope.IsAdmin) return true;

        return CanAccessCore(entity, scope, 0);
    }

    private static Boolean CanAccessCore(IEntity entity, ProjectScope scope, Int32 depth)
    {
        if (entity == null) return true;
        if (scope == null || scope.IsAdmin) return true;
        if (depth > 8) return false;

        var type = entity.GetType();
        var factory = type.AsFactory();
        if (factory == null) return true;

        switch (GetMode(type))
        {
            case ProjectScopeMode.Project:
                return scope.Contains(GetInt(entity, factory, "ProjectId"));

            case ProjectScopeMode.ProjectSelf:
                return scope.Contains(GetInt(entity, factory, "Id"));

            case ProjectScopeMode.AppId:
                return scope.ContainsApp(GetInt(entity, factory, "AppId"));

            case ProjectScopeMode.NodeId:
                return scope.ContainsNode(GetInt(entity, factory, "NodeId"));

            case ProjectScopeMode.Parent:
                var link = FindParentLink(factory);
                return link != null && CanAccessParent(GetInt(entity, factory, link.Value.Field), link.Value.Parent, scope, depth);

            case ProjectScopeMode.AdminOnly:
                return false;

            case ProjectScopeMode.Global:
            case ProjectScopeMode.None:
                return true;

            default:
                Warn(type);
                return false;
        }
    }

    private static Boolean CanAccessParent(Int32 parentId, Type parentType, ProjectScope scope, Int32 depth)
    {
        if (parentId <= 0 || parentType == null) return false;

        var parentFactory = parentType.AsFactory();
        if (parentFactory == null) return false;

        var parent = parentFactory.Find(parentFactory.Unique.Name, parentId);

        return parent != null && CanAccessCore(parent, scope, depth + 1);
    }

    private static Int32 GetInt(IEntity entity, IEntityFactory factory, String name)
    {
        if (entity is not EntityBase eb) return 0;

        var field = FindField(factory, name);
        if (field == null) return 0;

        return eb[field.Name].ToInt();
    }

    private static void SetInt(IEntity entity, IEntityFactory factory, String name, Int32 value)
    {
        if (entity is not EntityBase eb) return;

        var field = FindField(factory, name);
        if (field != null) eb[field.Name] = value;
    }
    #endregion

    #region 写入校验
    /// <summary>填充新增实体的项目编号</summary>
    /// <param name="entity">实体</param>
    /// <param name="scope">项目范围</param>
    public static void FillCreate(IEntity entity, ProjectScope scope)
    {
        if (entity == null || scope == null || scope.IsAdmin || scope.SelectedProjectId <= 0) return;

        var factory = entity.GetType().AsFactory();
        if (factory == null) return;
        if (GetMode(entity.GetType()) != ProjectScopeMode.Project) return;
        if (GetInt(entity, factory, "ProjectId") > 0) return;

        SetInt(entity, factory, "ProjectId", scope.SelectedProjectId);
    }

    /// <summary>校验写入是否越权。越权时抛出异常</summary>
    /// <param name="entity">实体</param>
    /// <param name="method">数据操作方法</param>
    /// <param name="scope">项目范围</param>
    /// <returns>是否通过</returns>
    public static Boolean Validate(IEntity entity, DataMethod method, ProjectScope scope)
    {
        if (entity == null || scope == null || scope.IsAdmin) return true;

        var factory = entity.GetType().AsFactory();
        if (factory == null) return true;

        var mode = GetMode(entity.GetType());
        if (mode == ProjectScopeMode.Global || mode == ProjectScopeMode.None) return true;

        if (method == DataMethod.Insert)
        {
            if (mode == ProjectScopeMode.Project)
            {
                var pid = GetInt(entity, factory, "ProjectId");
                if (pid <= 0 && scope.SelectedProjectId > 0)
                {
                    SetInt(entity, factory, "ProjectId", scope.SelectedProjectId);
                    pid = scope.SelectedProjectId;
                }
                if (pid <= 0) throw new InvalidOperationException(L.T("Please select a project."));
                if (!scope.Contains(pid)) throw new InvalidOperationException(L.T("No permission to create data in project [{0}].", pid));

                var insertAppId = GetInt(entity, factory, "AppId");
                if (insertAppId > 0 && ProjectScope.GetAppProjectId(insertAppId) != pid)
                    throw new InvalidOperationException(L.T("Application [{0}] does not belong to project [{1}].", insertAppId, pid));

                var insertNodeId = GetInt(entity, factory, "NodeId");
                if (insertNodeId > 0 && ProjectScope.GetNodeProjectId(insertNodeId) != pid)
                    throw new InvalidOperationException(L.T("Node [{0}] does not belong to project [{1}].", insertNodeId, pid));

                return true;
            }

            if (mode == ProjectScopeMode.ProjectSelf)
                throw new InvalidOperationException(L.T("Only system administrators can create projects."));
        }

        // 更新/删除时字段可能不完整，按主键回读后再校验
        var target = entity;
        if (method != DataMethod.Insert && entity is EntityBase eb && factory.Unique != null)
        {
            var id = eb[factory.Unique.Name].ToInt();
            if (id > 0 && IsKeyMissing(entity, factory, mode))
                target = factory.Find(factory.Unique.Name, id) ?? entity;
        }

        switch (mode)
        {
            case ProjectScopeMode.Project:
                var projectId = GetInt(target, factory, "ProjectId");
                if (!scope.Contains(projectId)) throw new InvalidOperationException(L.T("No permission to operate data in project [{0}].", projectId));

                // 同时带父级外键的实体，必须与项目保持一致，避免把他人应用挂到自己的项目下
                var linkedAppId = GetInt(target, factory, "AppId");
                if (linkedAppId > 0 && ProjectScope.GetAppProjectId(linkedAppId) != projectId)
                    throw new InvalidOperationException(L.T("Application [{0}] does not belong to project [{1}].", linkedAppId, projectId));

                var linkedNodeId = GetInt(target, factory, "NodeId");
                if (linkedNodeId > 0 && ProjectScope.GetNodeProjectId(linkedNodeId) != projectId)
                    throw new InvalidOperationException(L.T("Node [{0}] does not belong to project [{1}].", linkedNodeId, projectId));

                return true;

            case ProjectScopeMode.ProjectSelf:
                var selfId = GetInt(target, factory, "Id");
                if (!scope.Contains(selfId)) throw new InvalidOperationException(L.T("No permission to operate project [{0}].", selfId));
                return true;

            case ProjectScopeMode.AppId:
                var appId = GetInt(target, factory, "AppId");
                var appProjectId = ProjectScope.GetAppProjectId(appId);
                if (!scope.Contains(appProjectId)) throw new InvalidOperationException(L.T("No permission to operate application [{0}].", appId));
                return true;

            case ProjectScopeMode.NodeId:
                var nodeId = GetInt(target, factory, "NodeId");
                var nodeProjectId = ProjectScope.GetNodeProjectId(nodeId);
                if (!scope.Contains(nodeProjectId)) throw new InvalidOperationException(L.T("No permission to operate node [{0}].", nodeId));
                return true;

            case ProjectScopeMode.Parent:
                var link = FindParentLink(factory);
                if (link == null) throw new InvalidOperationException(L.T("No permission to operate this data."));
                var parentId = GetInt(target, factory, link.Value.Field);
                if (!CanAccessParent(parentId, link.Value.Parent, scope, 0)) throw new InvalidOperationException(L.T("No permission to operate this data."));
                return true;

            case ProjectScopeMode.AdminOnly:
                throw new InvalidOperationException(L.T("Only system administrators can operate this data."));

            default:
                Warn(entity.GetType());
                throw new InvalidOperationException(L.T("No permission to operate this data."));
        }
    }

    private static Boolean IsKeyMissing(IEntity entity, IEntityFactory factory, ProjectScopeMode mode)
    {
        switch (mode)
        {
            case ProjectScopeMode.Project:
                return GetInt(entity, factory, "ProjectId") <= 0;
            case ProjectScopeMode.ProjectSelf:
                return GetInt(entity, factory, "Id") <= 0;
            case ProjectScopeMode.AppId:
                return GetInt(entity, factory, "AppId") <= 0;
            case ProjectScopeMode.NodeId:
                return GetInt(entity, factory, "NodeId") <= 0;
            case ProjectScopeMode.Parent:
                var link = FindParentLink(factory);
                return link == null || GetInt(entity, factory, link.Value.Field) <= 0;
            default:
                return false;
        }
    }
    #endregion
}
