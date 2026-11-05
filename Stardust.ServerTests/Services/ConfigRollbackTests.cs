using System;
using NewLife;
using NewLife.Serialization;
using Stardust.Data.Configs;
using Xunit;

namespace ServerTest.Services;

/// <summary>配置回滚实体逻辑测试。回滚规则下沉 AppConfig.Rollback，Web/WebApi/MCP 共用同一实现。</summary>
public class ConfigRollbackTests
{
    [Fact(DisplayName = "配置回滚：恢复发布历史版本并写 Rollback 历史")]
    public void Rollback_RestoresHistoricalVersion()
    {
        var name = "rollback-" + Guid.NewGuid().ToString("n")[..8];
        var cfg = new AppConfig
        {
            Name = name,
            ProjectId = 999777,
            Enable = true,
            Version = 2,
            NextVersion = 3,
        };
        cfg.Insert();

        var snap = new AppConfig
        {
            Id = cfg.Id,
            Name = name,
            ProjectId = cfg.ProjectId,
            Version = 0,
            NextVersion = 1,
        };
        var history = ConfigHistory.Add(cfg.Id, "Publish", true, snap.ToJson());

        var data = new ConfigData
        {
            ConfigId = cfg.Id,
            Key = "Key1",
            Value = "current",
            Version = 2,
            Enable = true,
        };
        data.Insert();

        try
        {
            var rs = AppConfig.Rollback(history.Id);

            Assert.Equal(2, rs.Before);
            Assert.Equal(1, rs.Version);
            Assert.Equal(1, rs.Config.Version);
            Assert.Equal(1, rs.Config.NextVersion);

            // 旧实现按版本重发布；该行没有 NewVersion==1 的草稿，因此重发布 0 项
            Assert.Equal(0, rs.Republished);
            Assert.NotNull(rs.Config);

            var rollback = ConfigHistory.FindAll(ConfigHistory._.ConfigId == cfg.Id & ConfigHistory._.Action == "Rollback");
            Assert.Single(rollback);
            Assert.True(rollback[0].Success);
        }
        finally
        {
            ConfigHistory.Delete(ConfigHistory._.ConfigId == cfg.Id);
            ConfigData.Delete(ConfigData._.ConfigId == cfg.Id);
            cfg.Delete();
        }
    }

    [Fact(DisplayName = "配置回滚：非发布历史被拒绝")]
    public void Rollback_RejectsNonPublishHistory()
    {
        var name = "rollback-" + Guid.NewGuid().ToString("n")[..8];
        var cfg = new AppConfig { Name = name, ProjectId = 999777, Enable = true, Version = 1 };
        cfg.Insert();

        var history = ConfigHistory.Add(cfg.Id, "Update", true, cfg.ToJson());
        try
        {
            var ex = Assert.Throws<InvalidOperationException>(() => AppConfig.Rollback(history.Id));
            Assert.Contains("仅发布历史", ex.Message);
        }
        finally
        {
            ConfigHistory.Delete(ConfigHistory._.ConfigId == cfg.Id);
            cfg.Delete();
        }
    }
}
