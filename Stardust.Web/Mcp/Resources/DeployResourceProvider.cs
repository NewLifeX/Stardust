using Stardust.Data.Deployment;

namespace Stardust.Web.Mcp.Resources;

/// <summary>部署集资源Provider。为get_resource工具提供AppDeploy详情查询</summary>
public class DeployResourceProvider : IResourceProvider
{
    /// <summary>资源类型</summary>
    public String ResourceType => "deploy";

    /// <summary>按ID获取部署集详情</summary>
    public Task<Object> GetAsync(Int32 id)
    {
        var d = AppDeploy.FindById(id);
        if (d == null) return Task.FromResult<Object>(null!);

        return Task.FromResult<Object>(new
        {
            id = d.Id,
            project_id = d.ProjectId,
            app_id = d.AppId,
            name = d.Name,
            category = d.Category,
            enable = d.Enable,
            nodes = d.Nodes,
            version = d.Version,
            multi_version = d.MultiVersion,
            auto_publish = d.AutoPublish,
            package_name = d.PackageName,
            port = d.Port,
            urls = d.Urls,
            repository = d.Repository,
            branch = d.Branch,
            project_path = d.ProjectPath,
            project_kind = d.ProjectKind.ToString(),
            build_args = d.BuildArgs,
            package_filters = d.PackageFilters,
            file_name = d.FileName,
            arguments = d.Arguments,
            working_directory = d.WorkingDirectory,
            mode = d.Mode.ToString(),
            max_memory = d.MaxMemory,
            health_check = d.HealthCheck,
            remark = d.Remark,
            create_time = d.CreateTime,
        });
    }
}
