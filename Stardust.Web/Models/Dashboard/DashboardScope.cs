using Stardust.Data.Platform;

namespace Stardust.Web.Models.Dashboard;

/// <summary>可见项目范围解析。管理员取全部启用项目，普通用户按项目成员关系过滤</summary>
public static class DashboardScope
{
    /// <summary>解析可见范围</summary>
    /// <param name="projects">全部项目</param>
    /// <param name="isAdmin">是否管理员。系统角色不受数据范围限制</param>
    /// <param name="memberProjectIds">当前用户加入的项目</param>
    /// <returns>可见范围</returns>
    public static ScopeInfo Resolve(IEnumerable<GalaxyProject> projects, Boolean isAdmin, IEnumerable<Int32> memberProjectIds)
    {
        var all = (projects ?? []).Where(e => e != null && e.Enable && e.Id > 0).OrderBy(e => e.Sort).ThenBy(e => e.Id).ToList();
        var members = (memberProjectIds ?? []).Where(e => e > 0).Distinct().ToHashSet();

        List<GalaxyProject> visible;
        String mode;
        if (isAdmin)
        {
            visible = all;
            mode = visible.Count > 0 ? "full" : "none";
        }
        else
        {
            visible = all.Where(e => members.Contains(e.Id)).ToList();
            mode = visible.Count == 0 ? "none" : visible.Count < all.Count ? "limited" : "full";
        }

        return new ScopeInfo
        {
            IsAdmin = isAdmin,
            Mode = mode,
            TotalProjects = all.Count,
            VisibleProjects = visible.Count,
            ProjectIds = visible.Select(e => e.Id).ToArray(),
            Projects = visible.Select(e => new ProjectOption { Id = e.Id, Name = e.Name }).ToList(),
        };
    }
}
