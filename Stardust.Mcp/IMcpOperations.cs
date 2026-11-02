namespace Stardust.Mcp;

/// <summary>宿主写操作适配器。实现放在 Web/WebApi，复用已注册的 ConfigService、DeployService、PipelineService 与 StarFactory。</summary>
public interface IMcpOperations
{
    Task<Int32> SetConfigAsync(Int32 configId, IDictionary<String, Object> configs);
    Task<Int32> PublishConfigAsync(Int32 configId);
    Task<String> UseVersionAsync(Int32 versionId, String ip);
    Task<Int32> ControlDeployAsync(Int32 deployId, Int32 deployNodeId, String action, String ip);
    Task<Object> TriggerPipelineAsync(Int32 pipelineId, String ip);
    Task<Object> SendNodeCommandAsync(String nodeCode, String command, String args);
    Task<Object> SendAppCommandAsync(String appName, String clientId, String command, String args);
}
