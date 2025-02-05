using NewLife;
using NewLife.Serialization;
using Stardust.Data.Deployment;

namespace Stardust.Web.Services;

public class DeployService
{
    private readonly StarFactory _starFactory;

    public DeployService(StarFactory starFactory) => _starFactory = starFactory;

    public async Task Control(AppDeploy app, AppDeployNode deployNode, String action, String ip, Int32 startTime, Int32 timeout)
    {
        if (deployNode == null) throw new ArgumentNullException(nameof(deployNode));

        app ??= deployNode.Deploy;
        //if (app == null) throw new ArgumentNullException(nameof(deployNode));
        //if (!deployNode.Enable || app == null || !app.Enable) throw new Exception("部署节点未启用！");
        if (app == null || !app.Enable) throw new Exception($"节点[{deployNode}]上的应用部署集[{app}]未启用！");

        await Task.Yield();

        using var span = _starFactory.Tracer?.NewSpan($"Deploy-{action}", deployNode);

        var msg = "";
        var success = true;
        try
        {
            switch (action.ToLower())
            {
                case "install":
                    action = "deploy/install";
                    Install(deployNode);
                    break;
                case "start":
                    action = "deploy/start";
                    Start(deployNode);
                    break;
                case "stop":
                    action = "deploy/stop";
                    Stop(deployNode);
                    break;
                case "restart":
                    action = "deploy/restart";
                    Restart(deployNode);
                    break;
                case "uninstall":
                    action = "deploy/uninstall";
                    Uninstall(deployNode);
                    break;
                default:
                    throw new NotSupportedException($"不支持{action}");
            }

            // 发布安装命令时，为了兼容旧版本，继续传递AppName参数
            var deployName = deployNode.DeployName;
            if (deployName.IsNullOrEmpty()) deployName = app?.Name;

            var args = new { deployNode.Id, DeployName = deployName, AppName = app?.Name }.ToJson();
            msg = args;

            await _starFactory.SendNodeCommand(deployNode.Node.Code, action, args, startTime, startTime + 60, timeout);
        }
        catch (Exception ex)
        {
            span?.SetError(ex, null);
            msg = ex.Message;
            success = false;

            throw;
        }
        finally
        {
            var hi = AppDeployHistory.Create(deployNode.DeployId, deployNode.NodeId, action, success, msg, ip);
            hi.SaveAsync();
        }
    }

    public void Install(AppDeployNode deployNode)
    {
        deployNode.Enable = true;
        deployNode.Update();
    }

    public void Start(AppDeployNode deployNode)
    {
        deployNode.Enable = true;
        deployNode.Update();
    }

    public void Stop(AppDeployNode deployNode)
    {
        deployNode.Enable = false;
        deployNode.Update();
    }

    public void Restart(AppDeployNode deployNode)
    {
        deployNode.Enable = true;
        deployNode.Update();
    }

    public void Uninstall(AppDeployNode deployNode)
    {
        deployNode.Enable = false;
        deployNode.Update();
    }
}