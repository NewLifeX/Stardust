using Stardust.Data.Configs;

namespace Stardust.Mcp;

/// <summary>宿主写操作适配器。实现放在 Web/WebApi，复用已注册的 ConfigService、DeployService、PipelineService 与 StarFactory。</summary>
public interface IMcpOperations
{
    Task<Int32> SetConfigAsync(Int32 configId, IDictionary<String, Object> configs);
    Task<Int32> PublishConfigAsync(Int32 configId);

    /// <summary>回滚到指定发布历史。复用 ConfigService 的回滚与通知逻辑，不在 MCP 内直接操作实体。</summary>
    Task<ConfigRollbackResult> RollbackConfigAsync(Int32 historyId);
    /// <summary>切换部署集当前版本。publishEnabledNodes 为 true 时按自动发布向启用节点下发安装，与 MVC/API UseVersion 相同。</summary>
    Task<String> UseVersionAsync(Int32 versionId, String ip, Boolean publishEnabledNodes = true);
    Task<Int32> ControlDeployAsync(Int32 deployId, Int32 deployNodeId, String action, String ip);
    Task<Object> TriggerPipelineAsync(Int32 pipelineId, String ip);
    Task<Object> SendNodeCommandAsync(String nodeCode, String command, String args);
    Task<Object> SendAppCommandAsync(String appName, String clientId, String command, String args);

    /// <summary>列出部署节点配置目录下的日志文件（节点指令 deploy/log/list，经 DeployService 合并日志配置）。</summary>
    Task<Object> ReadDeployLogFilesAsync(Int32 deployNodeId);

    /// <summary>读取部署节点日志文件尾部（节点指令 deploy/log/tail，offset 支持增量追新）。</summary>
    Task<Object> ReadDeployLogTailAsync(Int32 deployNodeId, String file, Int64 offset, Int32 maxBytes);
}
