using Microsoft.AspNetCore.Mvc;
using NewLife;
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
    private App _app;
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

    protected override void OnWriteError(String action, String message) => WriteHistory(action, false, message);
    #endregion

    /// <summary>获取分配到本节点的应用服务信息</summary>
    /// <returns></returns>
    public ServiceInfo[] GetAll()
    {
        var list = AppDeployNode.FindAllByNodeId(_node.ID);

        var infos = list.Where(e => e.Enable).Select(e => e.ToService()).Where(e => e != null).ToArray();

        return infos;
    }

    private AppDeploy Valid(String appId, String secret, String clientId, String token)
    {
        // 优先令牌解码
        App ap = null;
        if (!token.IsNullOrEmpty())
        {
            var (jwt, ap1) = _tokenService.DecodeToken(token, _setting.TokenSecret);
            if (appId.IsNullOrEmpty()) appId = ap1?.Name;
            if (clientId.IsNullOrEmpty()) clientId = jwt.Id;

            ap = ap1;
        }

        if (ap == null) ap = _tokenService.Authorize(appId, secret, _setting.AutoRegister);

        // 新建应用
        var app = AppDeploy.FindById(ap.Id);
        if (app == null)
        {
            var obj = AppDeploy.Meta.Table;
            lock (obj)
            {
                app = AppDeploy.FindById(ap.Id);
                if (app == null)
                {
                    app = new AppDeploy
                    {
                        Id = ap.Id,
                        Enable = true,
                    };
                    app.Copy(ap);

                    app.Insert();
                }
            }
        }

        // 检查应用有效性
        if (!app.Enable) throw new ArgumentOutOfRangeException(nameof(appId), $"应用[{appId}]已禁用！");

        return app;
    }

    /// <summary>上传所有应用服务信息</summary>
    /// <param name="model"></param>
    /// <returns></returns>
    [HttpPost]
    public Int32 SetAll([FromBody] ServiceInfo[] model)
    {
        return 0;
    }

    #region 辅助
    private void WriteHistory(String action, Boolean success, String remark)
    {
        var hi = AppDeployHistory.Create(_app?.Id ?? 0, _node?.ID ?? 0, action, success, remark, UserHost);
        hi.SaveAsync();
    }
    #endregion
}