using System;
using System.Collections.Generic;
using System.Linq;
using NewLife;
using Stardust.Data.Nodes;
using XCode;
using XCode.Membership;

namespace Stardust.Data.Platform;

/// <summary>项目数据范围。表示当前用户可见的项目集合</summary>
public class ProjectScope
{
    #region 属性
    /// <summary>用户编号</summary>
    public Int32 UserId { get; set; }

    /// <summary>是否系统管理员。管理员不受项目范围限制</summary>
    public Boolean IsAdmin { get; set; }

    /// <summary>可见项目编号</summary>
    public Int32[] ProjectIds { get; set; } = [];

    /// <summary>当前选择项目。仅当落在可见范围内时有效</summary>
    public Int32 SelectedProjectId { get; set; }

    /// <summary>是否空范围。非管理员且没有任何项目授权</summary>
    public Boolean IsEmpty => !IsAdmin && (ProjectIds == null || ProjectIds.Length == 0);

    private HashSet<Int32> _appIds;
    private Int32[] _appIdArray;
    private HashSet<Int32> _nodeIds;
    private Int32[] _nodeIdArray;
    private readonly Dictionary<Type, Int32[]> _parentIds = [];
    private readonly Object _cacheLock = new();
    #endregion

    #region 判定
    /// <summary>判断项目是否在可见范围内</summary>
    /// <param name="projectId">项目编号</param>
    /// <returns>是否可见</returns>
    public Boolean Contains(Int32 projectId) => IsAdmin || (projectId > 0 && ProjectIds != null && Array.IndexOf(ProjectIds, projectId) >= 0);

    /// <summary>判断应用是否在可见范围内。列表过滤使用，首次调用加载允许的应用编号集合</summary>
    /// <param name="appId">应用编号</param>
    /// <returns>是否可见</returns>
    public Boolean ContainsApp(Int32 appId)
    {
        if (IsAdmin) return true;
        if (appId <= 0 || ProjectIds == null || ProjectIds.Length == 0) return false;

        EnsureAppIds();
        return _appIds.Contains(appId);
    }

    /// <summary>获取可见应用编号。跨库过滤时用于物化 IN 列表</summary>
    /// <returns>应用编号数组</returns>
    public Int32[] GetAllowedAppIds()
    {
        if (IsAdmin || ProjectIds == null || ProjectIds.Length == 0) return [];

        EnsureAppIds();
        return _appIdArray;
    }

    private void EnsureAppIds()
    {
        if (_appIds != null) return;

        lock (_cacheLock)
        {
            if (_appIds != null) return;

            var ids = Stardust.Data.App.FindAll(Stardust.Data.App._.ProjectId.In(ProjectIds)).Select(e => e.Id).Distinct().OrderBy(e => e).ToArray();
            _appIdArray = ids;
            _appIds = ids.ToHashSet();
        }
    }

    /// <summary>判断节点是否在可见范围内。列表过滤使用，首次调用加载允许的节点编号集合</summary>
    /// <param name="nodeId">节点编号</param>
    /// <returns>是否可见</returns>
    public Boolean ContainsNode(Int32 nodeId)
    {
        if (IsAdmin) return true;
        if (nodeId <= 0 || ProjectIds == null || ProjectIds.Length == 0) return false;

        EnsureNodeIds();
        return _nodeIds.Contains(nodeId);
    }

    /// <summary>获取可见节点编号。跨库过滤时用于物化 IN 列表</summary>
    /// <returns>节点编号数组</returns>
    public Int32[] GetAllowedNodeIds()
    {
        if (IsAdmin || ProjectIds == null || ProjectIds.Length == 0) return [];

        EnsureNodeIds();
        return _nodeIdArray;
    }

    private void EnsureNodeIds()
    {
        if (_nodeIds != null) return;

        lock (_cacheLock)
        {
            if (_nodeIds != null) return;

            var ids = Node.FindAll(Node._.ProjectId.In(ProjectIds)).Select(e => e.ID).Distinct().OrderBy(e => e).ToArray();
            _nodeIdArray = ids;
            _nodeIds = ids.ToHashSet();
        }
    }

    /// <summary>获取可见父级编号。跨库过滤时用于物化 IN 列表</summary>
    /// <param name="parentType">父级实体类型</param>
    /// <param name="loader">首次加载委托</param>
    /// <returns>父级编号数组</returns>
    public Int32[] GetParentIds(Type parentType, Func<Type, Int32[]> loader)
    {
        if (parentType == null) return [];
        if (IsAdmin || ProjectIds == null || ProjectIds.Length == 0) return [];

        lock (_cacheLock)
        {
            if (_parentIds.TryGetValue(parentType, out var ids)) return ids;

            ids = loader?.Invoke(parentType) ?? [];
            _parentIds[parentType] = ids;

            return ids;
        }
    }

    /// <summary>查询应用所属项目。写入校验使用，单条查询避免加载全部应用</summary>
    /// <param name="appId">应用编号</param>
    /// <returns>项目编号。应用不存在时返回 0</returns>
    public static Int32 GetAppProjectId(Int32 appId)
    {
        if (appId <= 0) return 0;

        return Stardust.Data.App.FindById(appId)?.ProjectId ?? 0;
    }

    /// <summary>查询节点所属项目。写入校验使用，单条查询避免加载全部节点</summary>
    /// <param name="nodeId">节点编号</param>
    /// <returns>项目编号。节点不存在时返回 0</returns>
    public static Int32 GetNodeProjectId(Int32 nodeId)
    {
        if (nodeId <= 0) return 0;

        return Node.FindByID(nodeId)?.ProjectId ?? 0;
    }
    #endregion

    #region 解析
    /// <summary>解析指定用户的可见项目范围</summary>
    /// <param name="userId">用户编号。小于等于零表示匿名</param>
    /// <returns>项目范围</returns>
    public static ProjectScope Resolve(Int32 userId)
    {
        var scope = new ProjectScope { UserId = userId };
        if (userId <= 0) return scope;

        var user = XCode.Membership.User.FindById(userId);
        if (user == null) return scope;

        scope.IsAdmin = IsSystemRole(user);
        if (scope.IsAdmin) return scope;

        var members = ProjectUser.FindAllByUserId(userId);
        if (members == null || members.Count == 0) return scope;

        var ids = members.Where(e => e != null && e.Enable && e.ProjectId > 0).Select(e => e.ProjectId).Distinct().ToList();
        if (ids.Count == 0) return scope;

        // 只保留启用项目；禁用项目不再放行
        var projects = GalaxyProject.FindAll();
        var enabled = projects.Where(e => e != null && e.Enable && ids.Contains(e.Id)).Select(e => e.Id).OrderBy(e => e).ToArray();
        scope.ProjectIds = enabled;

        return scope;
    }

    /// <summary>判断用户是否系统角色</summary>
    /// <param name="user">用户</param>
    /// <returns>是否系统角色</returns>
    public static Boolean IsSystemRole(XCode.Membership.User user)
    {
        if (user == null) return false;

        var roleIds = new List<Int32>();
        if (user.RoleId > 0) roleIds.Add(user.RoleId);
        if (!user.RoleIds.IsNullOrEmpty())
        {
            foreach (var item in user.RoleIds.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                if (Int32.TryParse(item.Trim(), out var id) && id > 0) roleIds.Add(id);
            }
        }

        if (roleIds.Count == 0) return false;

        var roles = Role.FindAll();
        return roles.Any(e => roleIds.Contains(e.Id) && e.IsSystem);
    }
    #endregion
}
