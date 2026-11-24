using System;
using System.IO;
using NewLife;
using Stardust.Data.Platform;
using Stardust.Models;

namespace Stardust.Data.Deployment;

/// <summary>应用包上传辅助。把可选的 star.json 与部署集合并为部署版本</summary>
public static class AppPackageHelper
{
    /// <summary>解析 zip 根目录的 star.json（可选），校验单应用与包名，并填充部署集运行参数</summary>
    /// <param name="entity">待保存的部署版本</param>
    /// <param name="stream">zip 数据流</param>
    /// <returns>读取到清单并完成填充时返回 true；zip 内没有 star.json 时返回 false</returns>
    public static Boolean ApplyStarPackage(AppDeployVersion entity, Stream stream)
    {
        if (entity == null) throw new ArgumentNullException(nameof(entity));
        if (stream == null) throw new ArgumentNullException(nameof(stream));

        var deploy = entity.Deploy;
        if (deploy == null) return false;

        var read = PackageManifestReader.TryRead(stream);
        if (read == null) return false;

        var manifest = read.Value.manifest;
        if (manifest.Apps == null || manifest.Apps.Length != 1)
            throw new InvalidOperationException(L.T("Server packages must contain exactly one application."));

        // 包名带项目前缀且部署集尚未归属项目时，自动关联项目，保证 FullName 与包名一致
        var expectedName = deploy.FullName;
        var index = manifest.Name?.LastIndexOf('/') ?? -1;
        if (index > 0 && deploy.ProjectId == 0)
        {
            var project = GalaxyProject.FindByName(manifest.Name![..index]);
            if (project != null)
            {
                deploy.ProjectId = project.Id;
                expectedName = manifest.Name;
            }
        }
        if (!manifest.Name.EqualIgnoreCase(expectedName) && !manifest.Name.EqualIgnoreCase(deploy.Name))
            throw new InvalidOperationException(L.T("Package name [{0}] does not match the deployment set [{1}].", manifest.Name, expectedName));

        entity.Version = manifest.Version!;
        entity.Enable = true;

        var os = manifest.ResolveOS();
        if (os != OSKind.Any) entity.OS = os;
        var arch = manifest.ResolveArch();
        if (arch != CpuArch.Any) entity.Arch = arch;

        var app = manifest.Apps[0];
        if (deploy.FileName.IsNullOrEmpty()) deploy.FileName = app.FileName;
        if (deploy.Arguments.IsNullOrEmpty()) deploy.Arguments = app.Arguments;
        if (deploy.WorkingDirectory.IsNullOrEmpty()) deploy.WorkingDirectory = app.WorkingDirectory;
        if (deploy.UserName.IsNullOrEmpty()) deploy.UserName = app.UserName;
        if (deploy.Environments.IsNullOrEmpty()) deploy.Environments = app.Environments;
        if (deploy.HealthCheck.IsNullOrEmpty()) deploy.HealthCheck = app.HealthCheck;
        if (deploy.MaxMemory == 0) deploy.MaxMemory = app.MaxMemory;
        if (deploy.Priority == 0) deploy.Priority = app.Priority;
        if (deploy.Mode == DeployMode.Default) deploy.Mode = app.Mode;
        if (deploy.Remark.IsNullOrEmpty()) deploy.Remark = manifest.Description;
        if (deploy.AppId == 0)
        {
            var appEntity = App.FindByName(deploy.Name);
            if (appEntity != null) deploy.AppId = appEntity.Id;
        }

        // 同版本多 RID/TFM 时，发布中心按节点平台选版本
        deploy.MultiVersion = true;
        deploy.Update();

        return true;
    }
}
