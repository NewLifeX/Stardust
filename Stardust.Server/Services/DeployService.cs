using NewLife;
using NewLife.Remoting.Models;
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

    public AppDeployVersion GetDeployVersion(AppDeploy app, Node node)
    {
        if (app.MultiVersion)
        {
            // 查找最新的一批版本，挑选符合目标节点的最新版本
            var vers = AppDeployVersion.FindAllByDeployId(app.Id, 100);
            vers = vers.Where(e => e.Enable).OrderByDescending(e => e.Id).ToList();

            // 目标节点所支持的运行时标识符。一般有两个，如 win-x64/win
            var rids = OSKindHelper.GetRID(node.OSKind, node.Architecture?.ToLower() + "");

            //return vers.FirstOrDefault(e => rids.Contains(e.Runtime));

            // 可能有多个版本，挑选最新的适合目标节点操作系统、指令集和框架运行时的版本
            var fms = node.Frameworks?.Split(',', StringSplitOptions.RemoveEmptyEntries) ?? [];
            foreach (var ver in vers)
            {
                if (!rids.Contains(ver.Runtime)) continue;
                if (!ver.TargetFramework.IsNullOrEmpty() && fms.Length > 0)
                {
                    var tfm = ver.TargetFramework.TrimStart("netcoreapp", "net", "v");

                    // 特殊处理4.x，例如net4.6.1可以运行在net4.7/net4.8上
                    if (tfm.StartsWith("4."))
                    {
                        var v = new Version(tfm);
                        if (!fms.Any(e => e.StartsWith("4.") && new Version(e) >= v))
                            continue;
                    }
                    else if (!fms.Any(e => e.StartsWith(tfm)))
                        continue;
                }

                return ver;
            }

            return null;
        }

        return AppDeployVersion.FindByDeployIdAndVersion(app.Id, app.Version);
    }

    public DeployInfo BuildDeployInfo(AppDeployNode item, Node node)
    {
        // 消除缓存，解决版本更新后不能及时更新缓存的问题
        var app = item.Deploy;
        app = AppDeploy.FindByKey(app.Id);
        if (app == null || !app.Enable) return null;

        //todo: 需要根据当前节点的处理器指令集和操作系统版本来选择合适的版本
        //var ver = AppDeployVersion.FindByDeployIdAndVersion(app.Id, app.Version);
        var ver = GetDeployVersion(app, node);
        if (ver == null) return null;

        var inf = new DeployInfo
        {
            Id = item.Id,
            Name = app.AppName ?? app.Name,
            Version = app.Version,
            Url = ver?.Url,
            Hash = ver?.Hash,
            Overwrite = ver?.Overwrite,
            Mode = ver.Mode,

            Service = item.ToService(app),
        };

        // 修正Url
        if (inf.Url.StartsWithIgnoreCase("/cube/file/")) inf.Url = inf.Url.Replace("/cube/file/", "/cube/file?id=");

        // 如果是dotnet应用，可能需要额外的参数
        if (app.ProjectKind == ProjectKinds.DotNet)
        {
            var port = item.Port;
            if (port <= 0) port = app.Port;
            if (port > 0)
            {
                var args = inf.Service.Arguments;
                if (args.IsNullOrEmpty() || !args.Contains("urls=", StringComparison.OrdinalIgnoreCase))
                    inf.Service.Arguments = (args + " urls=http://*:" + port).Trim();
            }
        }

        return inf;
    }

    /// <summary>更新应用部署的节点信息</summary>
    /// <param name="online"></param>
    public void UpdateDeployNode(AppOnline online)
    {
        if (online == null || online.AppId == 0 || online.NodeId == 0) return;

        // 剔除StarAgent
        if (online.AppName == "StarAgent") return;

        // 找应用部署。此时只有应用标识和节点标识，可能对应多个部署集
        var list = AppDeploy.FindAllByAppId(online.AppId);
        if (list.Count == 0)
        {
            // 根据应用名查找
            var deploy = AppDeploy.FindByName(online.AppName);
            if (deploy != null)
            {
                // 部署名绑定到别的应用，退出
                if (deploy.AppId != 0 && deploy.AppId != online.AppId) return;

                // 当顶当前应用
                deploy.AppId = online.AppId;
                deploy.Update();
            }
            else
            {
                // 新增部署集，禁用状态，信息不完整
                //deploy = new AppDeploy
                //{
                //    AppId = online.AppId,
                //    Name = online.AppName,
                //    Category = online.App?.Category
                //};
                //deploy.Insert();

                deploy = AppDeploy.GetOrAdd(online.AppName);
                deploy.AppId = online.AppId;
                deploy.Category = online.App?.Category;
                deploy.Save();
            }
            list.Add(deploy);
        }

        // 查找节点。借助缓存找到启用的那一个部署节点，去更新它的信息。如果有多个无法识别，则都更新一遍
        //var nodes = AppDeployNode.Search(list.Select(e => e.Id).ToArray(), online.NodeId, null, null);
        var nodes = list.SelectMany(e => e.DeployNodes).Where(e => e.NodeId == online.NodeId).ToList();
        var node = nodes.FirstOrDefault(e => e.Enable);

        // 自动创建部署节点，更新信息
        if (node != null)
        {
            node.Fill(online);
            node.Update();
        }
        else
        {
            // 由于无法确定发布集，所以创建所有发布集的节点。此时不能启用，否则下一次应用启动时，将会拉取到该部署信息，而此时部署信息还不完整
            foreach (var deploy in list)
            {
                node = nodes.FirstOrDefault(e => e.DeployId == deploy.Id);
                node ??= new AppDeployNode { DeployId = deploy.Id, NodeId = online.NodeId, Enable = false };
                node.Fill(online);
                node.Save();
            }
        }
        {
            // 定时更新部署信息
            foreach (var deploy in list)
            {
                if (deploy.UpdateTime.AddHours(1) < DateTime.Now) deploy.Fix();
            }
        }
    }

    public void WriteHistory(Int32 appId, Int32 nodeId, String action, Boolean success, String remark, String ip)
    {
        var hi = AppDeployHistory.Create(appId, nodeId, action, success, remark, ip);
        hi.Insert();
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
            var context = new DeviceContext
            {
                Device = ap,
                UserHost = ip,
                ClientId = clientId,
            };
            _registryService.OnPing(context, inf);
            AppMeter.WriteData(ap, inf, "Deploy", clientId, ip);
        }

        // 部署集
        var app = AppDeploy.FindByName(name);
        app ??= new AppDeploy { Name = name };
        if (app.AppId <= 0) app.AppId = ap.Id;
        if (!ap.Category.IsNullOrEmpty()) app.Category = ap.Category;
        app.Save();

        // 本节点所有发布
        var list = AppDeployNode.FindAllByNodeId(node.ID);
        var dn = list.FirstOrDefault(e => e.DeployId == app.Id);
        dn ??= new AppDeployNode { DeployId = app.Id, NodeId = node.ID };

        dn.Fill(inf);
        dn.LastActive = DateTime.Now;

        return dn.Update();
    }
}