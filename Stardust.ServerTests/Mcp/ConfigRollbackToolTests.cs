using System;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using NewLife.Serialization;
using Stardust.Data.Configs;
using Stardust.Mcp;
using Stardust.Mcp.Tools.Config;
using Xunit;

namespace ServerTest.Mcp;

/// <summary>config_rollback MCP 工具走宿主 IMcpOperations 的回归测试。
/// 2026-11-05 修复：工具旧实现直接改 AppConfig/ConfigData，与 WebApi ConfigHistoryController.Rollback 重复；
/// 现统一为 ConfigService.Rollback，MCP 只做项目隔离校验并委托宿主适配器。</summary>
public class ConfigRollbackToolTests
{
    private const Int32 ProjectId = 999778;

    [Fact(DisplayName = "config_rollback 只委托 IMcpOperations，不直接写实体")]
    public async Task Rollback_DelegatesToOperations()
    {
        var cfg = new AppConfig { Name = "mcp-rollback-" + Guid.NewGuid().ToString("n")[..8], ProjectId = ProjectId, Enable = true, Version = 2, NextVersion = 3 };
        cfg.Insert();

        var snap = new AppConfig { Id = cfg.Id, Name = cfg.Name, ProjectId = cfg.ProjectId, Version = 0, NextVersion = 1 };
        var history = ConfigHistory.Add(cfg.Id, "Publish", true, snap.ToJson());

        var result = new ConfigRollbackResult { Config = cfg, Before = 2, Version = 1, Republished = 3, Notified = 2 };
        var ops = new Mock<IMcpOperations>();
        ops.Setup(e => e.RollbackConfigAsync(history.Id)).ReturnsAsync(result);

        var services = new ServiceCollection();
        services.AddSingleton(ops.Object);
        using var provider = services.BuildServiceProvider();
        var ctx = new McpContext { TokenId = 999999, ProjectId = ProjectId, Services = provider };

        try
        {
            var content = await new ConfigRollbackTool().InvokeAsync(
                JsonSerializer.SerializeToElement(new { history_id = history.Id }), ctx);

            var json = JsonSerializer.Serialize(content);
            Assert.Contains("\"version\":1", json);
            Assert.Contains("\"republished\":3", json);
            Assert.Contains("\"notified\":2", json);

            ops.Verify(e => e.RollbackConfigAsync(history.Id), Times.Once);

            // 工具本身不落 Rollback 历史；写操作全部由宿主服务执行
            var rollback = ConfigHistory.FindAll(ConfigHistory._.ConfigId == cfg.Id & ConfigHistory._.Action == "Rollback");
            Assert.Empty(rollback);
        }
        finally
        {
            ConfigHistory.Delete(ConfigHistory._.ConfigId == cfg.Id);
            cfg.Delete();
        }
    }
}
