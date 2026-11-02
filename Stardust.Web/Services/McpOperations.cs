using NewLife;
using Stardust.Data.Configs;
using Stardust.Data.Deployment;
using Stardust.Mcp;
using Stardust.Server.Services;
using Stardust.Web.Services;

namespace Stardust.Web.Services;

/// <summary>MCP 写操作适配器。复用宿主已注册的配置、部署、流水线服务和命令通道。</summary>
public class McpOperations : IMcpOperations
{
    private readonly ConfigService _config;
    private readonly DeployService _deploy;
    private readonly PipelineService _pipeline;
    private readonly StarFactory _star;

    public McpOperations(ConfigService config, DeployService deploy, PipelineService pipeline, StarFactory star)
    {
        _config = config;
        _deploy = deploy;
        _pipeline = pipeline;
        _star = star;
    }

    public Task<Int32> SetConfigAsync(Int32 configId, IDictionary<String, Object> configs)
    {
        var cfg = AppConfig.FindById(configId) ?? throw new InvalidOperationException("配置不存在");
        return Task.FromResult(_config.SetConfigs(cfg, configs));
    }

    public Task<Int32> PublishConfigAsync(Int32 configId) => _config.Publish(configId);

    public async Task<String> UseVersionAsync(Int32 versionId, String ip)
    {
        var ver = AppDeployVersion.FindById(versionId) ?? throw new InvalidOperationException("版本不存在");
        if (!ver.Enable || ver.Version.IsNullOrEmpty() || ver.Url.IsNullOrEmpty())
            throw new InvalidOperationException("版本未启用、没有版本号或没有包地址");
        var app = ver.Deploy ?? AppDeploy.FindById(ver.DeployId);
        app.Version = ver.Version;
        app.Update();
        if (app.Enable && app.AutoPublish)
        {
            foreach (var node in AppDeployNode.FindAllByAppId(app.Id).Where(e => e.Enable))
                await _deploy.Control(app, node, "install", ip, node.Delay, 0);
            return "已切换版本，并按自动发布向启用节点下发安装";
        }
        return "已切换版本号，未开启自动发布，未下发节点";
    }

    public async Task<Int32> ControlDeployAsync(Int32 deployId, Int32 deployNodeId, String action, String ip)
    {
        var app = AppDeploy.FindById(deployId);
        var node = AppDeployNode.FindById(deployNodeId);
        return await _deploy.Control(app, node, action, ip, 0, 0);
    }

    public async Task<Object> TriggerPipelineAsync(Int32 pipelineId, String ip)
    {
        var run = await _pipeline.Trigger(pipelineId, ip);
        return new { run_id = run.Id, status = run.Status.ToString() };
    }

    public async Task<Object> SendNodeCommandAsync(String nodeCode, String command, String args)
    {
        var reply = await _star.SendNodeCommandAsync(nodeCode, command, args, 0, 3600, 5);
        return new { status = reply?.Status.ToString(), data = reply?.Data };
    }

    public async Task<Object> SendAppCommandAsync(String appName, String clientId, String command, String args)
    {
        var reply = await _star.SendAppCommandAsync(appName, clientId, command, args, 0, 3600, 5);
        return new { status = reply?.Status.ToString(), data = reply?.Data };
    }
}
