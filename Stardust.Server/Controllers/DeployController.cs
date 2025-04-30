using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using NewLife;
using NewLife.Remoting.Extensions;
using NewLife.Serialization;
using Stardust.Data.Deployment;
using Stardust.Data.Nodes;
using Stardust.Models;
using Stardust.Server.Services;
using TokenService = Stardust.Server.Services.TokenService;

namespace Stardust.Server.Controllers;

/// <summary>发布中心服务</summary>
[ApiFilter]
[Route("[controller]/[action]")]
public class DeployController : BaseController
{
    private Node _node;
    private String _clientId;
    private readonly NodeService _nodeService;
    private readonly DeployService _deployService;
    private readonly TokenService _tokenService;
    private readonly StarServerSetting _setting;

    public DeployController(DeployService deployService, NodeService nodeService, TokenService tokenService, StarServerSetting setting, IServiceProvider serviceProvider) : base(serviceProvider)
    {
        _deployService = deployService;
        _nodeService = nodeService;
        _tokenService = tokenService;
        _setting = setting;
    }

    #region 令牌验证
    protected override Boolean OnAuthorize(String token)
    {
        var (jwt, node, ex) = _nodeService.DecodeToken(token, _setting.TokenSecret);
        _node = node;
        _clientId = jwt.Id;
        if (ex != null) throw ex;

        return node != null;
    }

    protected override void OnWriteError(String action, String message) => WriteHistory(0, action, false, message);
    #endregion

    /// <summary>获取分配到本节点的应用服务信息</summary>
    /// <returns></returns>
    public DeployInfo[] GetAll(Int32 deployId, String deployName, String appName)
    {
        var list = AppDeployNode.FindAllByNodeId(_node.ID);

        var rs = new List<DeployInfo>();
        foreach (var item in list)
        {
            // 不返回未启用的发布集，如果需要在客户端删除，则通过指令下发来实现
            if (!item.Enable) continue;

            // 过滤需要的应用部署
            if (deployId > 0 && item.Id != deployId) continue;

            var app = item.Deploy;
            if (app == null || !app.Enable) continue;
            if (!deployName.IsNullOrEmpty())
            {
                if (!item.DeployName.IsNullOrEmpty() && item.DeployName != deployName ||
                    item.DeployName.IsNullOrEmpty() && app.Name != deployName) continue;
            }
            if (!appName.IsNullOrEmpty() && app.AppName != appName) continue;

            // 消除缓存，解决版本更新后不能及时更新缓存的问题
            app = AppDeploy.FindByKey(app.Id);
            if (app == null || !app.Enable) continue;

            //todo: 需要根据当前节点的处理器指令集和操作系统版本来选择合适的版本
            //var ver = AppDeployVersion.FindByDeployIdAndVersion(app.Id, app.Version);
            var ver = _deployService.GetDeployVersion(app, _node);
            if (ver == null) continue;

            var inf = new DeployInfo
            {
                Id = item.Id,
                Name = app.Name,
                Version = app.Version,
                Url = ver?.Url,
                Hash = ver?.Hash,
                Overwrite = ver?.Overwrite,
                Mode = ver.Mode,

                Service = item.ToService(app),
            };
            rs.Add(inf);

            // 修正Url
            if (inf.Url.StartsWithIgnoreCase("/cube/file/")) inf.Url = inf.Url.Replace("/cube/file/", "/cube/file?id=");

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
        var list = AppDeployNode.FindAllByNodeId(_node.ID);

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

            //// 仅可用应用
            //if (app.Enable)
            {
                if (app.FileName.IsNullOrEmpty()) app.FileName = svc.FileName;
                if (app.Arguments.IsNullOrEmpty()) app.Arguments = svc.Arguments;
                if (app.WorkingDirectory.IsNullOrEmpty()) app.WorkingDirectory = svc.WorkingDirectory;
                if (app.UserName.IsNullOrEmpty()) app.UserName = svc.UserName;
                if (app.Environments.IsNullOrEmpty()) app.Environments = svc.Environments;
                if (app.Mode < 0) app.Mode = svc.Mode;

                app.MaxMemory = svc.MaxMemory;
            }

            // 先保存，可能有插入，需要取得应用发布Id
            var rs2 = app.Save();

            if (rs2 > 0) WriteHistory(app.Id, nameof(Upload), true, svc.ToJson());

            rs += rs2;

            dn ??= list.FirstOrDefault(e => e.DeployId == app.Id);
            if (dn == null)
                dn = new AppDeployNode { DeployId = app.Id, NodeId = _node.ID, Enable = svc.Enable };
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
    public Int32 Ping([FromBody] AppInfo inf) => _deployService.Ping(_node, inf, UserHost);

    #region 辅助
    private void WriteHistory(Int32 appId, String action, Boolean success, String remark) => _deployService.WriteHistory(appId, _node?.ID ?? 0, action, success, remark, UserHost);
    #endregion
}