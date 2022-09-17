using NewLife.Serialization;
using Stardust.Data.Deployment;

namespace Stardust.Web.Services;

public class DeployService
{
    private readonly StarFactory _starFactory;

    public DeployService(StarFactory starFactory) => _starFactory = starFactory;

    public async Task Control(AppDeployNode deployNode, String action, String ip)
    {
        if (deployNode == null) throw new ArgumentNullException(nameof(deployNode));

        var app = deployNode.App;
        //if (app == null) throw new ArgumentNullException(nameof(deployNode));
        //if (!deployNode.Enable || app == null || !app.Enable) throw new Exception("部署节点未启用！");
        if (app == null || !app.Enable) throw new Exception("部署节点未启用！");

        var msg = "";
        var success = true;
        try
        {
            switch (action.ToLower())
            {
                case "publish":
                    action = "deploy/publish";
                    Publish(deployNode);
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
                default:
                    throw new NotSupportedException($"不支持{action}");
            }

            var args = new { deployNode.Id, deployNode.AppName }.ToJson();
            msg = args;

            await _starFactory.SendNodeCommand(deployNode.Node.Code, action, args);
        }
        catch (Exception ex)
        {
            msg = ex.Message;
            success = false;

            throw;
        }
        finally
        {
            var hi = AppDeployHistory.Create(deployNode.AppId, deployNode.NodeId, nameof(Publish), success, msg, ip);
            hi.SaveAsync();
        }
    }

    public void Publish(AppDeployNode deployNode)
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

    }
}