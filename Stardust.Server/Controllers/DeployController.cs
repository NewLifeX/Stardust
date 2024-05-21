using Microsoft.AspNetCore.Mvc;
using NewLife;
using NewLife.Serialization;
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
    private readonly StarServerSetting _setting;

    public DeployController(DeployService deployService, NodeService nodeService, TokenService tokenService, StarServerSetting setting)
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
        //指定节点上所有应用
        var appsInNode = AppDeployNode.FindAllByNodeId(_node.ID);
        var rs = new List<DeployInfo>();

        foreach (var app in appsInNode)
        {
            // 不返回未启用的发布集
            if (!app.Enable) continue;

            var appDeploy = app.Deploy;
            if (appDeploy == null || !appDeploy.Enable) continue;

            //取所有, Id.Desc
            var versions = AppDeployVersion.FindAllByDeployId(appDeploy.Id);
            if (!versions.Any()) continue;
            
            versions = versions.Where(ver => ver.Enable == true) //未启用的直接过滤
                .GroupBy(ver => new { ver.DeployName, ver.Strategy }) //以 DeployName & Strategy 分组 Group by DeployName and Strategy combination
                .SelectMany(group => group.OrderBy(ver => versions.IndexOf(ver)).Take(1)) //保留组内索引最小的一个，也就是最新的Select the first element of each group
                .ToList();

            foreach (var ver in versions)
            {
                //应用策略
                if (!ver.Match(_node))
                {
                    WriteHistory(appDeploy.Id, nameof(GetAll), false, ver.MatchResult(_node));
                    continue;
                }

                var inf = new DeployInfo
                {
                    Name = appDeploy.Name,
                    Version = ver.Version,
                    Url = ver?.Url,
                    Hash = ver?.Hash,
                    Overwrite = ver?.Overwrite,

                    Service = app.ToService(appDeploy),
                };

                rs.Add(inf);

                // 修正Url
                if (inf.Url.StartsWithIgnoreCase("/cube/file/")) inf.Url = inf.Url.Replace("/cube/file/", "/cube/file?id=");

                WriteHistory(appDeploy.Id, nameof(GetAll), true, inf.ToJson());
            }
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

            var app = AppDeploy.FindByName(svc.Name);
            app ??= new AppDeploy { Name = svc.Name/*, Enable = svc.Enable*/ };

            //// 仅可用应用
            //if (app.Enable)
            {
                if (app.FileName.IsNullOrEmpty()) app.FileName = svc.FileName;
                if (app.Arguments.IsNullOrEmpty()) app.Arguments = svc.Arguments;
                if (app.WorkingDirectory.IsNullOrEmpty()) app.WorkingDirectory = svc.WorkingDirectory;
                if (app.UserName.IsNullOrEmpty()) app.UserName = svc.UserName;
                if (app.Mode < 0) app.Mode = svc.Mode;

                app.MaxMemory = svc.MaxMemory;
            }

            // 先保存，可能有插入，需要取得应用发布Id
            var rs2 = app.Save();

            if (rs2 > 0) WriteHistory(app.Id, nameof(Upload), true, svc.ToJson());

            rs += rs2;

            var dn = list.FirstOrDefault(e => e.DeployId == app.Id);
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