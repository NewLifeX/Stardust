using NewLife;
using Stardust.Data.Deployment;

namespace Stardust.Web.Mcp.Resources;

/// <summary>流水线资源Provider。为get_resource工具提供AppPipeline详情查询</summary>
public class PipelineResourceProvider : IResourceProvider
{
    /// <summary>资源类型</summary>
    public String ResourceType => "pipeline";

    /// <summary>按ID获取流水线详情</summary>
    public Task<Object> GetAsync(Int32 id)
    {
        var p = AppPipeline.FindById(id);
        if (p == null) return Task.FromResult<Object>(null!);

        return Task.FromResult<Object>(new
        {
            id = p.Id,
            name = p.Name,
            deploy_id = p.DeployId,
            project_id = p.ProjectId,
            branch = p.Branch,
            build_node_id = p.BuildNodeId,
            deploy_node_ids = p.DeployNodeIds,
            auto_deploy = p.AutoDeploy,
            enable = p.Enable,
            has_secret = !p.Secret.IsNullOrEmpty(),
            has_token = !p.Token.IsNullOrEmpty(),
            trace_id = p.TraceId,
            remark = p.Remark,
            create_time = p.CreateTime,
        });
    }
}
