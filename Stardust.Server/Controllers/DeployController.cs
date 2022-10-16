using Microsoft.AspNetCore.Mvc;
using NewLife;
using NewLife.Serialization;
using Stardust.Data;
using Stardust.Data.Deployment;
using Stardust.Data.Nodes;
using Stardust.Models;
using Stardust.Server.Common;
using Stardust.Server.Services;
using TokenService = Stardust.Server.Services.TokenService;

namespace Stardust.Server.Controllers;

/// <summary>发布中心服务</summary>
[ApiFilter]
[Route("[controller]/[action]")]
public class DeployController : BaseController
{
    private Node _node;
    private readonly NodeService _nodeService;
    private readonly DeployService _deployService;
    private readonly TokenService _tokenService;
    private readonly Setting _setting;

    public DeployController(DeployService deployService, NodeService nodeService, TokenService tokenService, Setting setting)
    {
        _deployService = deployService;
        _nodeService = nodeService;
        _tokenService = tokenService;
        _setting = setting;
    }

    #region 令牌验证
    protected override Boolean OnAuthorize(String token)
    {
        var (node, ex) = _nodeService.DecodeToken(token, _setting.TokenSecret);
        _node = node;
        if (ex != null) throw ex;

        return node != null;
    }

    protected override void OnWriteError(String action, String message) => WriteHistory(0, action, false, message);
    #endregion

    /// <summary>获取分配到本节点的应用服务信息</summary>
    /// <returns></returns>
    public DeployInfo[] GetAll()
    {
        var list = AppDeployNode.FindAllByNodeId(_node.ID);

        var rs = new List<DeployInfo>();
        foreach (var item in list)
        {
            //// 不返回未启用的发布集
            //if (!item.Enable) continue;

            var app = item.App;
            if (app == null /*|| !app.Enable*/) continue;

            var ver = AppDeployVersion.FindByAppIdAndVersion(app.Id, app.Version);

            var inf = new DeployInfo
            {
                Name = item.AppName,
                Version = app.Version,
                Url = ver?.Url,
                Hash = ver?.Hash,

                Service = item.ToService(),
            };
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
        var list = AppDeployNode.FindAllByNodeId(_node.ID);

        var rs = 0;
        foreach (var svc in services)
        {
            var app = AppDeploy.FindByName(svc.Name);
            app ??= new AppDeploy { Name = svc.Name, Enable = svc.Enable };

            // 仅可用应用
            if (app.Enable)
            {
                if (app.FileName.IsNullOrEmpty()) app.FileName = svc.FileName;
                if (app.Arguments.IsNullOrEmpty()) app.Arguments = svc.Arguments;
                if (app.WorkingDirectory.IsNullOrEmpty()) app.WorkingDirectory = svc.WorkingDirectory;

                //app.Enable = svc.Enable;
                //app.AutoStart = svc.AutoStart;
                //app.AutoStop = svc.AutoStop;
                app.MaxMemory = svc.MaxMemory;
            }

            // 先保存，可能有插入，需要取得应用发布Id
            var rs2 = app.Save();

            if (rs2 > 0) WriteHistory(app.Id, nameof(Upload), true, svc.ToJson());

            rs += rs2;

            var dn = list.FirstOrDefault(e => e.AppId == app.Id);
            dn ??= new AppDeployNode { AppId = app.Id, NodeId = _node.ID, Enable = svc.Enable };

            if (svc.Enable) dn.Enable = true;

            dn.Save();
        }

        return rs;
    }

    #region 辅助
    private void WriteHistory(Int32 appId, String action, Boolean success, String remark)
    {
        var hi = AppDeployHistory.Create(appId, _node?.ID ?? 0, action, success, remark, UserHost);
        hi.SaveAsync();
    }
    #endregion
}