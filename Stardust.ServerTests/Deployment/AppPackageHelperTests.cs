using System;
using System.IO;
using System.IO.Compression;
using NewLife;
using NewLife.Security;
using Stardust.Data.Deployment;
using Stardust.Data.Platform;
using Xunit;

namespace ServerTest.Deployment;

/// <summary>应用包上传辅助测试。覆盖可选清单、项目关联、单应用校验与运行参数回填</summary>
public class AppPackageHelperTests
{
    private static Stream CreateZip(String? manifest, Int32 appCount = 1)
    {
        var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true))
        {
            if (manifest != null)
            {
                var entry = zip.CreateEntry("star.json");
                using var writer = new StreamWriter(entry.Open());
                writer.Write(manifest);
            }

            for (var i = 0; i < appCount; i++)
            {
                var entry = zip.CreateEntry($"app{i}.exe");
                using var writer = new StreamWriter(entry.Open());
                writer.Write("dummy");
            }
        }

        stream.Position = 0;
        return stream;
    }

    private static (GalaxyProject project, AppDeploy deploy) CreateDeploy()
    {
        var project = new GalaxyProject { Name = "pkg_" + Rand.NextString(8), Enable = true };
        project.Insert();

        var deploy = new AppDeploy
        {
            ProjectId = project.Id,
            Name = "StarServer_" + Rand.NextString(6),
            Category = "test",
            Enable = true,
        };
        deploy.Insert();

        return (project, deploy);
    }

    private static void Cleanup(GalaxyProject project, AppDeploy deploy)
    {
        foreach (var item in AppDeployVersion.FindAllByDeployId(deploy.Id, 1000)) item.Delete();
        deploy.Delete();
        project.Delete();
    }

    [Fact(DisplayName = "可选清单：自动关联项目并回填运行参数")]
    public void ApplyStarPackage_FillsDeploy()
    {
        var (project, deploy) = CreateDeploy();
        var entity = new AppDeployVersion { DeployId = deploy.Id };
        var manifest = $$"""
        {
          "schema": 1,
          "name": "{{project.Name}}/{{deploy.Name}}",
          "version": "1.0",
          "defaultMode": "daemon",
          "os": 2,
          "arch": 2,
          "parameters": [ { "name": "port", "default": "6600" } ],
          "apps": [
            {
              "name": "starserver",
              "fileName": "StarServer.dll",
              "arguments": "--urls http://*:${port}"
            }
          ]
        }
        """;

        try
        {
            using var stream = CreateZip(manifest);
            var applied = AppPackageHelper.ApplyStarPackage(entity, stream);

            Assert.True(applied);

            var saved = AppDeploy.FindById(deploy.Id);
            Assert.Equal("1.0", entity.Version);
            Assert.Equal("StarServer.dll", saved.FileName);
            Assert.Equal("--urls http://*:${port}", saved.Arguments);
            Assert.True(saved.MultiVersion);
            // 上传只准备版本，不自动应用；必须由管理员点“使用版本”
            Assert.True(saved.Version.IsNullOrEmpty());
            Assert.Equal(2, (Int32)entity.OS);
            Assert.Equal(2, (Int32)entity.Arch);
        }
        finally
        {
            Cleanup(project, deploy);
        }
    }

    [Fact(DisplayName = "没有 star.json 时返回 false，保持部署集原配置")]
    public void ApplyStarPackage_NoManifest_ReturnsFalse()
    {
        var (project, deploy) = CreateDeploy();

        try
        {
            using var stream = CreateZip(null);
            var applied = AppPackageHelper.ApplyStarPackage(new AppDeployVersion { DeployId = deploy.Id }, stream);

            Assert.False(applied);
            Assert.Null(deploy.FileName);
            Assert.False(deploy.MultiVersion);
        }
        finally
        {
            Cleanup(project, deploy);
        }
    }

    [Fact(DisplayName = "清单含多个应用时拒绝上传")]
    public void ApplyStarPackage_MultipleApps_Throws()
    {
        var (project, deploy) = CreateDeploy();
        var manifest = $$"""
        {
          "schema": 1,
          "name": "{{project.Name}}/{{deploy.Name}}",
          "version": "1.0",
          "apps": [
            { "name": "one", "fileName": "app0.exe" },
            { "name": "two", "fileName": "app1.exe" }
          ]
        }
        """;

        try
        {
            using var stream = CreateZip(manifest, 2);

            var ex = Assert.Throws<InvalidOperationException>(() => AppPackageHelper.ApplyStarPackage(new AppDeployVersion { DeployId = deploy.Id }, stream));
            Assert.Contains("exactly one application", ex.Message);
        }
        finally
        {
            Cleanup(project, deploy);
        }
    }
}
