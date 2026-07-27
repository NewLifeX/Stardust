using Stardust.Data.Platform;

namespace Stardust.Web.Mcp.Resources;

/// <summary>项目资源Provider。为get_resource工具提供GalaxyProject详情查询</summary>
public class ProjectResourceProvider : IResourceProvider
{
    /// <summary>资源类型</summary>
    public String ResourceType => "project";

    /// <summary>按ID获取项目详情</summary>
    public Task<Object> GetAsync(Int32 id)
    {
        var p = GalaxyProject.FindById(id);
        if (p == null) return Task.FromResult<Object>(null!);

        return Task.FromResult<Object>(new
        {
            id = p.Id,
            name = p.Name,
            enable = p.Enable,
            tenant_id = p.TenantId,
            manager_id = p.ManagerId,
            nodes = p.Nodes,
            apps = p.Apps,
            is_global = p.IsGlobal,
            white_ips = p.WhiteIPs,
            black_ips = p.BlackIPs,
            remark = p.Remark,
            create_time = p.CreateTime,
        });
    }
}
