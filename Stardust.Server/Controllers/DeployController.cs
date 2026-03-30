using System.Runtime.Intrinsics.Arm;
using Microsoft.AspNetCore.Mvc;
using NewLife;
using NewLife.Log;
using NewLife.Remoting;
using NewLife.Remoting.Extensions;
using NewLife.Serialization;
using Stardust.Data.Deployment;
using Stardust.Data.Nodes;
using Stardust.Models;
using Stardust.Server.Services;
using Attachment = Stardust.Data.Deployment.Attachment;

namespace Stardust.Server.Controllers;

/// <summary>发布中心服务</summary>
[ApiFilter]
[Route("[controller]/[action]")]
public class DeployController(NodeService nodeService, DeployService deployService, IServiceProvider serviceProvider) : BaseController(nodeService, null, serviceProvider)
{
    private readonly Version _newDeploy = new(3, 7);

    /// <summary>获取分配到本节点的应用服务信息</summary>
    /// <returns></returns>
    public DeployInfo[] GetAll(Int32 deployId, String deployName, String appName)
    {
        var node = Context.Device as Node;
        var list = AppDeployNode.FindAllByNodeId(node.ID);

        var rs = new List<DeployInfo>();
        foreach (var deployNode in list)
        {
            // 不返回未启用的发布集，如果需要在客户端删除，则通过指令下发来实现
            if (!deployNode.Enable) continue;

            // 过滤需要的应用部署
            if (deployId > 0 && deployNode.Id != deployId) continue;

            var app = deployNode.Deploy;
            if (app == null || !app.Enable) continue;

            if (!deployName.IsNullOrEmpty())
            {
                if (!deployNode.DeployName.IsNullOrEmpty() && deployNode.DeployName != deployName ||
                    deployNode.DeployName.IsNullOrEmpty() && app.Name != deployName) continue;
            }
            if (!appName.IsNullOrEmpty() && app.AppName != appName) continue;

            // 修正旧的用户名
            deployNode.FixOldUserName();

            var inf = deployService.BuildDeployInfo(deployNode, node);
            if (inf == null) continue;

            // 如果客户版本较低，Mode需要转为旧版本服务模式
            if (Version.TryParse(node.Version, out var ver) && ver < _newDeploy)
            {
                inf.Service.Mode = (DeployMode)DeployModesExtensions.Convert(inf.Service.Mode);
            }

            rs.Add(inf);
            WriteHistory(app.Id, nameof(GetAll), true, inf.ToJson());
        }

        return rs.ToArray();
    }

    /// <summary>上传本节点的所有应用服务信息</summary>
    /// <param name="services"></param>
    /// <returns></returns>
    [HttpPost]
    public Int32 Upload([FromBody] ServiceInfo[] services)
    {
        if (services == null || services.Length == 0) return 0;

        // 本节点所有发布
        var node = Context.Device as Node;
        var list = AppDeployNode.FindAllByNodeId(node.ID);

        var rs = 0;
        foreach (var svc in services)
        {
            if (svc.Name.IsNullOrEmpty()) continue;

            AppDeploy app = null;

            // 发布节点可能有自定义名字
            var dn = list.FirstOrDefault(e => e.DeployName.EqualIgnoreCase(svc.Name));
            app = dn?.Deploy;

            app ??= AppDeploy.FindByName(svc.Name);
            app ??= new AppDeploy { Name = svc.Name/*, Enable = svc.Enable*/ };

            // 仅新应用或停用应用（新增后未使用）更新应用信息
            if (app.Id == 0 || !app.Enable)
            {
                app.FileName = svc.FileName;
                app.Arguments = svc.Arguments;
                app.WorkingDirectory = svc.WorkingDirectory;
                app.Environments = svc.Environments;
                //app.Mode = svc.Mode;
                app.AutoStop = svc.AutoStop;
                app.ReloadOnChange = svc.ReloadOnChange;
                app.MaxMemory = svc.MaxMemory;
                app.Priority = svc.Priority;
                app.HealthCheck = svc.HealthCheck;

                // 客户端使用旧版本时，需要转换为新版本
                var mode = svc.Mode;
                if (mode.IsNewVersion())
                    app.Mode = mode;
                else
                {
                    var mode2 = (ServiceModes)mode;
                    app.Mode = DeployModesExtensions.Convert(mode2);
                    if (mode2 == ServiceModes.Multiple) app.AllowMultiple = true;
                }

                // 新增时才记录应用部署的用户名，避免Windows/Linux混合部署时整个应用记住了Linux的用户名
                if (app.Id == 0)
                {
                    if (app.UserName.IsNullOrEmpty()) app.UserName = svc.UserName;
                }
            }

            // 先保存，可能有插入，需要取得应用发布Id
            var rs2 = app.Save();

            if (rs2 > 0) WriteHistory(app.Id, nameof(Upload), true, svc.ToJson());

            rs += rs2;

            dn ??= list.FirstOrDefault(e => e.DeployId == app.Id);
            if (dn == null)
                dn = new AppDeployNode { DeployId = app.Id, NodeId = node.ID, Enable = svc.Enable };
            else
                list.Remove(dn);

            if (svc.Enable && app.Enable) dn.Enable = true;
            dn.LastUpload = DateTime.Now;

            dn.Save();
        }

        // 没有匹配的应用，本次禁用
        foreach (var item in list)
        {
            item.Enable = false;
            item.LastUpload = DateTime.Now;
            item.Update();
        }

        return rs;
    }

    /// <summary>应用心跳。上报应用信息</summary>
    /// <param name="inf"></param>
    /// <returns></returns>
    [HttpPost]
    public Int32 Ping([FromBody] AppInfo inf) => deployService.Ping(Context.Device as Node, inf, UserHost);

    /// <summary>获取分配到本节点的应用发布任务</summary>
    public BuildTask GetBuildTask(Int32 deployId, String deployName, String appName)
    {
        return null;
    }

    /// <summary>更新编译任务</summary>
    /// <param name="result"></param>
    [HttpPost]
    public Int32 UpdateBuildTask(BuildResult result)
    {
        var ver = AppDeployVersion.FindById(result.Id)
            ?? throw new ArgumentNullException(nameof(result));

        if (!result.CommitId.IsNullOrEmpty()) ver.CommitId = result.CommitId;
        if (!result.CommitLog.IsNullOrEmpty()) ver.CommitLog = result.CommitLog;
        if (result.CommitTime.Year > 2000) ver.CommitTime = result.CommitTime;
        if (!result.Progress.IsNullOrEmpty()) ver.Progress = result.Progress;

        return ver.Update();
    }

    /// <summary>上传编译产物包。编译节点编译完成后，上传zip包并自动创建应用版本</summary>
    /// <param name="deployName">应用部署集名称</param>
    /// <param name="version">版本号</param>
    /// <param name="commitId">提交标识</param>
    /// <param name="commitLog">提交记录</param>
    /// <param name="commitTime">提交时间</param>
    /// <param name="file">zip文件</param>
    /// <returns></returns>
    [HttpPost]
    public async Task<Object> UploadBuildFile(String deployName, String version, String? commitId, String? commitLog, String? commitTime, [FromForm] IFormFile file)
    {
        if (deployName.IsNullOrEmpty()) throw new ApiException(400, "应用部署集名称不能为空");
        if (version.IsNullOrEmpty()) throw new ApiException(400, "版本号不能为空");
        if (file == null || file.Length == 0) throw new ApiException(400, "上传文件不能为空");

        var node = Context.Device as Node;

        // 查找应用部署集
        var app = AppDeploy.FindByName(deployName);
        if (app == null) throw new ApiException(404, $"应用部署集[{deployName}]不存在");

        var set = StarServerSetting.Current;

        // 创建或更新版本
        var ver = AppDeployVersion.FindByDeployIdAndVersion(app.Id, version);
        if (ver == null)
        {
            ver = new AppDeployVersion
            {
                DeployId = app.Id,
                Version = version,
                Enable = true,
            };
        }

        ver.TraceId = DefaultSpan.Current?.TraceId;

        // 设置Git提交信息
        if (!commitId.IsNullOrEmpty()) ver.CommitId = commitId;
        if (!commitLog.IsNullOrEmpty()) ver.CommitLog = commitLog;
        if (!commitTime.IsNullOrEmpty()) ver.CommitTime = commitTime.ToDateTime();

        // 创建附件并保存文件
        var att = new Attachment
        {
            Category = "AppDeploy",
            Key = app.Name,
            Title = app.Name,
            FileName = file.FileName,
            ContentType = file.ContentType,
            UploadTime = DateTime.Now,
        };

        using var stream = file.OpenReadStream();
        await att.SaveFile(stream, set.UploadPath).ConfigureAwait(false);

        ver.Url = $"/cube/file?id={att.Id}{att.Extension}";
        ver.Hash = att.Hash;
        ver.Size = att.Size;

        ver.Save();
        app.Fix();

        WriteHistory(app.Id, nameof(UploadBuildFile), true, $"版本={version} 大小={att.Size:n0} 节点={node?.Name}");

        return new { ver.Id, ver.Version, ver.Url, ver.Size, ver.Hash };
    }

    #region 辅助
    private void WriteHistory(Int32 appId, String action, Boolean success, String remark) => deployService.WriteHistory(appId, (Context.Device as Node)?.ID ?? 0, action, success, remark, UserHost);
    #endregion
}