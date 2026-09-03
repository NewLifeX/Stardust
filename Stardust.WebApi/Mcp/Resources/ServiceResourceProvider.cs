using Stardust.Data;
using Stardust.Data.Platform;

namespace Stardust.Web.Mcp.Resources;

/// <summary>服务资源Provider。为get_resource工具提供AppService详情查询</summary>
public class ServiceResourceProvider : IResourceProvider
{
    /// <summary>资源类型</summary>
    public String ResourceType => McpResourceType.Service.ToWireName();

    /// <summary>按ID获取服务详情</summary>
    public Task<Object> GetAsync(Int32 id)
    {
        var s = AppService.FindById(id);
        if (s == null) return Task.FromResult<Object>(null!);

        return Task.FromResult<Object>(new
        {
            id = s.Id,
            app_id = s.AppId,
            service_id = s.ServiceId,
            service_name = s.ServiceName,
            client = s.Client,
            node_id = s.NodeId,
            enable = s.Enable,
            version = s.Version,
            weight = s.Weight,
            scope = s.Scope,
            tag = s.Tag,
            address = s.Address,
            origin_address = s.OriginAddress,
            external_address = s.ExternalAddress,
            ping_count = s.PingCount,
            healthy = s.Healthy,
            check_times = s.CheckTimes,
            last_check = s.LastCheck,
            check_result = s.CheckResult,
            create_time = s.CreateTime,
        });
    }
}
