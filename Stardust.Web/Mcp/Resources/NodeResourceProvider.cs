using Stardust.Data.Nodes;

namespace Stardust.Web.Mcp.Resources;

/// <summary>节点资源Provider。为get_resource工具提供Node详情查询</summary>
public class NodeResourceProvider : IResourceProvider
{
    /// <summary>资源类型</summary>
    public String ResourceType => "node";

    /// <summary>按ID获取节点详情</summary>
    public Task<Object> GetAsync(Int32 id)
    {
        var n = Node.FindByID(id);
        if (n == null) return Task.FromResult<Object>(null!);

        return Task.FromResult<Object>(new
        {
            id = n.ID,
            project_id = n.ProjectId,
            name = n.Name,
            code = n.Code,
            enable = n.Enable,
            product_code = n.ProductCode,
            category = n.Category,
            version = n.Version,
            compile_time = n.CompileTime,
            os = n.OS,
            os_kind = n.OSKind.ToString(),
            architecture = n.Architecture,
            machine_name = n.MachineName,
            ip = n.IP,
            cpu = n.Cpu,
            memory = n.Memory,
            total_size = n.TotalSize,
            drive_size = n.DriveSize,
            last_active = n.LastActive,
            online_time = n.OnlineTime,
            remark = n.Remark,
            create_time = n.CreateTime,
        });
    }
}
