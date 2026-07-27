using Stardust.Data;

namespace Stardust.Web.Mcp.Resources;

/// <summary>应用资源Provider。为get_resource工具提供App详情查询</summary>
public class AppResourceProvider : IResourceProvider
{
    /// <summary>资源类型</summary>
    public String ResourceType => "app";

    /// <summary>按ID获取应用详情</summary>
    public Task<Object> GetAsync(Int32 id)
    {
        var a = App.FindById(id);
        if (a == null) return Task.FromResult<Object>(null!);

        return Task.FromResult<Object>(new
        {
            id = a.Id,
            project_id = a.ProjectId,
            name = a.Name,
            display_name = a.DisplayName,
            category = a.Category,
            enable = a.Enable,
            auto_active = a.AutoActive,
            version = a.Version,
            compile = a.Compile,
            period = a.Period,
            singleton = a.Singleton,
            white_ips = a.WhiteIPs,
            black_ips = a.BlackIPs,
            allow_control_nodes = a.AllowControlNodes,
            last_login = a.LastLogin,
            last_ip = a.LastIP,
            remark = a.Remark,
            create_time = a.CreateTime,
        });
    }
}
