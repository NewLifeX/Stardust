using NewLife;
using System.Xml.Linq;
using Stardust.Data;
using Stardust.Data.Deployment;
using Stardust.Data.Nodes;
using Stardust.Models;

namespace Stardust.Server.Services;

public class DeployService
{
    private readonly RegistryService _registryService;

    public DeployService(RegistryService registryService)
    {
        _registryService = registryService;
    }

    /// <summary>更新应用部署的节点信息</summary>
    /// <param name="online"></param>
    public void UpdateDeployNode(AppOnline online)
    {
        if (online == null || online.AppId == 0 || online.NodeId == 0) return;

        // 提出StarAgent
        if (online.AppName == "StarAgent") return;

        // 找应用部署
        var list = AppDeploy.FindAllByAppId(online.AppId);
        var deploy = list.FirstOrDefault();
        if (deploy == null)
        {
            // 根据应用名查找
            deploy = AppDeploy.FindByName(online.AppName);
            if (deploy != null)
            {
                // 部署名绑定到别的应用，退出
                if (deploy.AppId != 0 && deploy.AppId != online.AppId) return;
            }
            else
            {
                // 新增部署集，禁用状态，信息不完整
                deploy = new AppDeploy
                {
                    AppId = online.AppId,
                    Name = online.AppName,
                    Category = online.App?.Category
                };
                deploy.Insert();
            }
        }

        // 查找节点。借助缓存
        var node = deploy.DeployNodes.FirstOrDefault(e => e.NodeId == online.NodeId);

        // 多个部署集，选一个
        if (node == null && list.Count > 1)
        {
            var nodes = AppDeployNode.Search(list.Select(e => e.Id).ToArray(), online.NodeId, null, null);
            //var node = nodes.FirstOrDefault(e => e.NodeId == online.NodeId);
            node = nodes.FirstOrDefault();
        }

        // 自动创建部署节点，更新信息
        node ??= new AppDeployNode { AppId = deploy.Id, NodeId = online.NodeId, Enable = false };

        node.IP = online.IP;
        node.ProcessId = online.ProcessId;
        node.ProcessName = online.ProcessName;
        node.UserName = online.UserName;
        node.StartTime = online.StartTime;
        node.Version = online.Version;
        node.Compile = online.Compile;
        node.LastActive = online.UpdateTime;

        node.Save();

        // 定时更新部署信息
        if (deploy.UpdateTime.AddHours(1) < DateTime.Now) deploy.Fix();
    }

    public void WriteHistory(Int32 appId, Int32 nodeId, String action, Boolean success, String remark, String ip)
    {
        var hi = AppDeployHistory.Create(appId, nodeId, action, success, remark, ip);
        hi.SaveAsync();
    }

    public Int32 Ping(Node node, AppInfo inf, String ip)
    {
        var name = !inf.AppName.IsNullOrEmpty() ? inf.AppName : inf.Name;
        if (name.IsNullOrEmpty()) return -1;

        // 应用
        var ap = App.FindByName(name);
        if (ap == null)
        {
            ap = new App { Name = name };
            ap.Insert();
        }
        {
            var clientId = $"{inf.IP?.Split(',').FirstOrDefault()}@{inf.Id}";
            _registryService.Ping(ap, inf, ip, clientId, null);
            AppMeter.WriteData(ap, inf, clientId, ip);
        }

        // 部署集
        var app = AppDeploy.FindByName(name);
        app ??= new AppDeploy { Name = name };
        app.AppId = ap.Id;
        if (!ap.Category.IsNullOrEmpty()) app.Category = ap.Category;
        app.Save();

        // 本节点所有发布
        var list = AppDeployNode.FindAllByNodeId(node.ID);
        var dn = list.FirstOrDefault(e => e.AppId == app.Id);
        dn ??= new AppDeployNode { AppId = app.Id, NodeId = node.ID };

        dn.Fill(inf);
        dn.LastActive = DateTime.Now;

        return dn.Update();
    }
}