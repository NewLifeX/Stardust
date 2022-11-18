using Stardust.Data;
using Stardust.Data.Deployment;

namespace Stardust.Server.Services;

public class DeployService
{
    /// <summary>更新应用部署的节点信息</summary>
    /// <param name="online"></param>
    public void UpdateDeployNode(AppOnline online)
    {
        if (online == null || online.AppId == 0 || online.NodeId == 0) return;

        // 找应用部署
        var deploy = AppDeploy.FindById(online.AppId);
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
                deploy = new AppDeploy { AppId = online.AppId, Name = online.AppName, Category = online.App?.Category };
                deploy.Insert();
            }
        }

        var nodes = AppDeployNode.FindAllByAppId(deploy.Id);
        var node = nodes.FirstOrDefault(e => e.NodeId == online.NodeId);

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
}