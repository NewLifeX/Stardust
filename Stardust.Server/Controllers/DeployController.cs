using System.Linq;
using System.Runtime.Intrinsics.Arm;
using Microsoft.AspNetCore.Mvc;
using NewLife;
using NewLife.Log;
using NewLife.Remoting;
using NewLife.Remoting.Extensions;
using NewLife.Serialization;
using Stardust.Data;
using Stardust.Data.Deployment;
using Stardust.Data.Nodes;
using Stardust.Models;
using Stardust.Server.Services;
using Attachment = Stardust.Data.Deployment.Attachment;

namespace Stardust.Server.Controllers;

/// <summary>发布中心服务</summary>
[ApiFilter]
[Route("[controller]/[action]")]
public class DeployController(NodeService nodeService, DeployService deployService, IServiceProvider serviceProvider, DownloadTokenService downloadTokenService) : BaseController(nodeService, null, serviceProvider)
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

            // 安全V-11：下发的部署包下载URL附带短时令牌
            if (!inf.Url.IsNullOrEmpty())
                inf.Url = downloadTokenService.AppendToken(inf.Url, StarServerSetting.Current.AttachmentTokenExpire);

            rs.Add(inf);
            WriteHistory(app.Id, HistoryAction.GetAll, true, inf.ToJson());
        }

        return rs.ToArray();
    }

    /// <summary>获取分配给本节点的证书信息。只返回与该节点关联且启用的证书</summary>
    /// <param name="certId">证书编号。0 表示该节点全部关联证书</param>
    /// <returns></returns>
    public CertInfo[] GetCerts(Int32 certId)
    {
        var node = Context.Device as Node;
        if (node == null) return [];

        var list = new List<CertInfo>();
        var linked = false;
        foreach (var link in SslCertificateNode.FindAllByNodeId(node.ID))
        {
            if (!link.Enable) continue;
            if (certId > 0 && link.CertId != certId) continue;

            var cert = link.Cert ?? SslCertificate.FindById(link.CertId);
            if (cert == null || !cert.Enable) continue;

            linked = true;

            var inf = BuildCertInfo(cert, link, node);
            if (inf != null) list.Add(inf);
        }

        if (certId > 0 && !linked) throw new ApiException(404, L.T("Certificate [{0}] is not associated with node [{1}].", certId, node.Name));

        return list.ToArray();
    }

    /// <summary>构造证书下发信息。按节点操作系统选择证书格式，并给下载地址附加短时令牌</summary>
    private CertInfo BuildCertInfo(SslCertificate cert, SslCertificateNode link, Node node)
    {
        var inf = new CertInfo
        {
            Id = cert.Id,
            Domain = cert.Domain,
            Name = cert.GetCertName(),
            TargetPath = link.GetTargetPath(),
            Version = cert.GetVersion(),
        };

        if (OSKindHelper.ToOSKind(node.OSKind) == OSKind.Windows)
        {
            if (!cert.PfxFile.IsNullOrEmpty())
            {
                inf.PfxFile = cert.PfxFile;
                inf.PfxHash = GetAttachmentHash(cert.PfxFile);
            }
        }
        else
        {
            var certFile = !cert.PemFile.IsNullOrEmpty() ? cert.PemFile : cert.CrtFile;
            if (!certFile.IsNullOrEmpty())
            {
                inf.CertFile = certFile;
                inf.CertHash = GetAttachmentHash(certFile);
            }
            if (!cert.KeyFile.IsNullOrEmpty())
            {
                inf.KeyFile = cert.KeyFile;
                inf.KeyHash = GetAttachmentHash(cert.KeyFile);
            }
        }

        // 没有可下发的文件
        if (inf.CertFile.IsNullOrEmpty() && inf.PfxFile.IsNullOrEmpty()) return null;

        // 安全V-11：私钥下载地址附带短时令牌
        var expire = StarServerSetting.Current.AttachmentTokenExpire;
        if (!inf.CertFile.IsNullOrEmpty()) inf.CertFile = downloadTokenService.AppendToken(inf.CertFile, expire);
        if (!inf.KeyFile.IsNullOrEmpty()) inf.KeyFile = downloadTokenService.AppendToken(inf.KeyFile, expire);
        if (!inf.PfxFile.IsNullOrEmpty()) inf.PfxFile = downloadTokenService.AppendToken(inf.PfxFile, expire);

        return inf;
    }

    /// <summary>从附件下载地址取得文件哈希</summary>
    private static String GetAttachmentHash(String url)
    {
        var id = Stardust.Server.Services.DownloadTokenService.GetAttachmentId(url);
        return id > 0 ? Attachment.FindById(id)?.Hash : null;
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

            if (rs2 > 0) WriteHistory(app.Id, HistoryAction.Upload, true, svc.ToJson());

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
    // 此值的意义是确保 multipart 解析不成为瓶颈（设为1GB），真正的上传包大小上限由入口处的 Startup 中间件控制：
    // 中间件根据 StarServerSetting.MaxUploadSize 动态设置 Kestrel 的 IHttpMaxRequestBodySizeFeature，
    // 在请求到达 Controller 之前即可拒绝超限请求，此特性仅作为 multipart 解析的上限兜底
    [RequestFormLimits(MultipartBodyLengthLimit = 1 * 1024 * 1024 * 1024)]
    public async Task<Object> UploadBuildFile(String deployName, String version, String? commitId, String? commitLog, String? commitTime, [FromForm] IFormFile file)
    {
        if (deployName.IsNullOrEmpty()) throw new ApiException(400, L.T("The application deployment set name cannot be empty."));
        if (version.IsNullOrEmpty()) throw new ApiException(400, L.T("Version cannot be empty."));
        if (file == null || file.Length == 0) throw new ApiException(400, L.T("The uploaded file cannot be empty."));

        var node = Context.Device as Node;

        // 查找应用部署集
        var app = AppDeploy.FindByName(deployName);
        if (app == null) throw new ApiException(404, L.T("Application deployment set [{0}] does not exist.", deployName));

        // 安全V-4：仅允许该发布集的编译/部署关联节点上传构建产物，防止任意已注册节点覆盖升级包（供应链投毒）
        var nodeId = node?.ID ?? 0;
        if (nodeId <= 0) throw new ApiException(ApiCode.Forbidden, L.T("Unable to identify the uploading node."));
        var buildNodes = AppBuildNode.FindAll(AppBuildNode._.DeployId == app.Id);
        var deployNodes = AppDeployNode.FindAllByDeployId(app.Id);
        if (!buildNodes.Any(e => e.Enable && e.NodeId == nodeId) && !deployNodes.Any(e => e.Enable && e.NodeId == nodeId))
            throw new ApiException(ApiCode.Forbidden, L.T("Node [{0}] is not a build/deploy node of deployment set [{1}]; uploading build artifacts is forbidden. Configure this node in the deployment set.", node?.Name, app.Name));

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

        WriteHistory(app.Id, HistoryAction.UploadBuildFile, true, L.TLog("Version={0} Size={1:n0} Node={2}", version, att.Size, node?.Name));

        return new { ver.Id, ver.Version, ver.Url, ver.Size, ver.Hash };
    }

    #region 辅助
    private void WriteHistory(Int32 appId, String action, Boolean success, String remark) => deployService.WriteHistory(appId, (Context.Device as Node)?.ID ?? 0, action, success, remark, UserHost);
    #endregion
}