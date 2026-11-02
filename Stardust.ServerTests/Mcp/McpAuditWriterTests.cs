using System.Text.Json;
using Moq;
using Stardust.Data.Platform;
using Stardust.Mcp;
using Xunit;

namespace ServerTest.Mcp;

/// <summary>
/// McpAuditWriter 特征测试。覆盖本轮修复：
/// <list type="bullet">
///   <item>S5: Begin() 写入 RiskLevel/ProjectId/ScopesUsed 三个新字段（McpAuditWriter.cs:27-29）</item>
///   <item>审计写路径脱敏: SanitizeArguments 把 password/secret/token 等值打码（McpAuditWriter.cs:56-68）</item>
/// </list>
/// 测试连真实 SQLite，Begin() 会真写一行 McpAudit，finally 清理。
/// </summary>
public class McpAuditWriterTests
{
    private static McpContext MakeCtx(Int32 projectId = 900001) => new()
    {
        TokenId = 999998,
        TokenName = "test-token",
        UserId = 0,
        ProjectId = projectId,
        Scopes = new HashSet<String>(StringComparer.OrdinalIgnoreCase) { "config:read", "config:write" },
        CallerIp = "127.0.0.1",
        UserAgent = "test-agent",
        TraceId = "test-trace-001",
    };

    private static Mock<IMcpTool> MakeTool(String name = "stardust_config_set", McpRiskLevel risk = McpRiskLevel.L1)
    {
        var mock = new Mock<IMcpTool>();
        mock.SetupGet(t => t.Name).Returns(name);
        mock.SetupGet(t => t.Description).Returns("test tool");
        mock.SetupGet(t => t.Risk).Returns(risk);
        mock.SetupGet(t => t.ScopeDomain).Returns("config");
        mock.SetupGet(t => t.RequireScope).Returns(true);
        mock.SetupGet(t => t.InputSchema).Returns(JsonSerializer.SerializeToElement(new { type = "object" }));
        return mock;
    }

    [Fact(DisplayName = "S5: Begin() 写入 RiskLevel")]
    public void Begin_SetsRiskLevel()
    {
        var ctx = MakeCtx();
        var tool = MakeTool(risk: McpRiskLevel.L1).Object;

        // 清理可能残留的同 TraceId 行
        foreach (var e in McpAudit.FindAll(McpAudit._.TraceId == "test-trace-001")) e.Delete();

        try
        {
            var audit = McpAuditWriter.Begin(ctx, tool, "{}");
            Assert.Equal((Int32)McpRiskLevel.L1, audit.RiskLevel);
        }
        finally
        {
            foreach (var e in McpAudit.FindAll(McpAudit._.TraceId == "test-trace-001")) e.Delete();
        }
    }

    [Fact(DisplayName = "S5: Begin() 写入 ProjectId")]
    public void Begin_SetsProjectId()
    {
        var ctx = MakeCtx(projectId: 888888);
        var tool = MakeTool().Object;

        foreach (var e in McpAudit.FindAll(McpAudit._.TraceId == "test-trace-001")) e.Delete();

        try
        {
            var audit = McpAuditWriter.Begin(ctx, tool, "{}");
            Assert.Equal(888888, audit.ProjectId);
        }
        finally
        {
            foreach (var e in McpAudit.FindAll(McpAudit._.TraceId == "test-trace-001")) e.Delete();
        }
    }

    [Fact(DisplayName = "S5: Begin() 写入 ScopesUsed（逗号拼接）")]
    public void Begin_SetsScopesUsed()
    {
        var ctx = MakeCtx();
        var tool = MakeTool().Object;

        foreach (var e in McpAudit.FindAll(McpAudit._.TraceId == "test-trace-001")) e.Delete();

        try
        {
            var audit = McpAuditWriter.Begin(ctx, tool, "{}");
            Assert.Contains("config:read", audit.ScopesUsed);
            Assert.Contains("config:write", audit.ScopesUsed);
        }
        finally
        {
            foreach (var e in McpAudit.FindAll(McpAudit._.TraceId == "test-trace-001")) e.Delete();
        }
    }

    [Fact(DisplayName = "审计脱敏: arguments 里 password 值被打码")]
    public void Begin_SanitizesPasswordValue()
    {
        var ctx = MakeCtx();
        var tool = MakeTool().Object;
        var args = "{\"key\":\"Db_Password\",\"value\":\"SuperSecret123\"}";

        foreach (var e in McpAudit.FindAll(McpAudit._.TraceId == "test-trace-001")) e.Delete();

        try
        {
            var audit = McpAuditWriter.Begin(ctx, tool, args);
            // 原值不应出现在落盘的 Arguments
            Assert.DoesNotContain("SuperSecret123", audit.Arguments);
            // 应有打码
            Assert.Contains("***", audit.Arguments);
        }
        finally
        {
            foreach (var e in McpAudit.FindAll(McpAudit._.TraceId == "test-trace-001")) e.Delete();
        }
    }

    [Fact(DisplayName = "审计脱敏: arguments 里 apiKey 值被打码")]
    public void Begin_SanitizesApiKeyValue()
    {
        var ctx = MakeCtx();
        var tool = MakeTool().Object;
        var args = "{\"apiKey\":\"sk-live-abc123\",\"name\":\"app1\"}";

        foreach (var e in McpAudit.FindAll(McpAudit._.TraceId == "test-trace-001")) e.Delete();

        try
        {
            var audit = McpAuditWriter.Begin(ctx, tool, args);
            Assert.DoesNotContain("sk-live-abc123", audit.Arguments);
            Assert.Contains("***", audit.Arguments);
            // 普通值不被打码
            Assert.Contains("app1", audit.Arguments);
        }
        finally
        {
            foreach (var e in McpAudit.FindAll(McpAudit._.TraceId == "test-trace-001")) e.Delete();
        }
    }

    [Fact(DisplayName = "审计脱敏: 普通参数值保留")]
    public void Begin_KeepsNormalValues()
    {
        var ctx = MakeCtx();
        var tool = MakeTool().Object;
        var args = "{\"key\":\"AppName\",\"value\":\"MyApp\"}";

        foreach (var e in McpAudit.FindAll(McpAudit._.TraceId == "test-trace-001")) e.Delete();

        try
        {
            var audit = McpAuditWriter.Begin(ctx, tool, args);
            Assert.Contains("AppName", audit.Arguments);
            Assert.Contains("MyApp", audit.Arguments);
        }
        finally
        {
            foreach (var e in McpAudit.FindAll(McpAudit._.TraceId == "test-trace-001")) e.Delete();
        }
    }

    [Fact(DisplayName = "审计脱敏: 空参数不抛异常")]
    public void Begin_NullArguments_DoesNotThrow()
    {
        var ctx = MakeCtx();
        var tool = MakeTool().Object;

        foreach (var e in McpAudit.FindAll(McpAudit._.TraceId == "test-trace-001")) e.Delete();

        try
        {
            var audit = McpAuditWriter.Begin(ctx, tool, null);
            Assert.Null(audit.Arguments);
        }
        finally
        {
            foreach (var e in McpAudit.FindAll(McpAudit._.TraceId == "test-trace-001")) e.Delete();
        }
    }

    [Fact(DisplayName = "Finish() 回写 Success 和 ErrorMessage")]
    public void Finish_UpdatesSuccessAndError()
    {
        var ctx = MakeCtx();
        var tool = MakeTool().Object;

        foreach (var e in McpAudit.FindAll(McpAudit._.TraceId == "test-trace-001")) e.Delete();

        try
        {
            var audit = McpAuditWriter.Begin(ctx, tool, "{}");
            McpAuditWriter.Finish(audit, false, "boom", 1234);

            var reloaded = McpAudit.FindById(audit.Id);
            Assert.False(reloaded.Success);
            Assert.Equal("boom", reloaded.ErrorMessage);
            Assert.Equal(1234, reloaded.Duration);
        }
        finally
        {
            foreach (var e in McpAudit.FindAll(McpAudit._.TraceId == "test-trace-001")) e.Delete();
        }
    }

    [Fact(DisplayName = "G4: Begin() 把调用前入参快照写入 BeforeValue")]
    public void Begin_SetsBeforeValue()
    {
        var ctx = MakeCtx();
        var tool = MakeTool().Object;
        var args = "{\"key\":\"AppName\",\"value\":\"MyApp\"}";

        foreach (var e in McpAudit.FindAll(McpAudit._.TraceId == "test-trace-001")) e.Delete();

        try
        {
            var audit = McpAuditWriter.Begin(ctx, tool, args);
            Assert.Contains("AppName", audit.BeforeValue);
            Assert.Contains("MyApp", audit.BeforeValue);
        }
        finally
        {
            foreach (var e in McpAudit.FindAll(McpAudit._.TraceId == "test-trace-001")) e.Delete();
        }
    }

    [Fact(DisplayName = "G4: BeforeValue 与 Arguments 同样脱敏（敏感值打码）")]
    public void Begin_BeforeValueSanitized()
    {
        var ctx = MakeCtx();
        var tool = MakeTool().Object;
        var args = "{\"key\":\"Db_Password\",\"value\":\"SuperSecret123\"}";

        foreach (var e in McpAudit.FindAll(McpAudit._.TraceId == "test-trace-001")) e.Delete();

        try
        {
            var audit = McpAuditWriter.Begin(ctx, tool, args);
            Assert.DoesNotContain("SuperSecret123", audit.BeforeValue);
            Assert.Contains("***", audit.BeforeValue);
        }
        finally
        {
            foreach (var e in McpAudit.FindAll(McpAudit._.TraceId == "test-trace-001")) e.Delete();
        }
    }

    [Fact(DisplayName = "G4: Finish() 传入 afterValue 时回写 AfterValue 并脱敏")]
    public void Finish_SetsAfterValueSanitized()
    {
        var ctx = MakeCtx();
        var tool = MakeTool().Object;

        foreach (var e in McpAudit.FindAll(McpAudit._.TraceId == "test-trace-001")) e.Delete();

        try
        {
            var audit = McpAuditWriter.Begin(ctx, tool, "{}");
            McpAuditWriter.Finish(audit, true, null, 88, "{\"summary\":\"已写入\",\"data\":{\"token\":\"abc-123\"}}");

            var reloaded = McpAudit.FindById(audit.Id);
            Assert.Contains("已写入", reloaded.AfterValue);
            Assert.DoesNotContain("abc-123", reloaded.AfterValue);
            Assert.Contains("***", reloaded.AfterValue);
        }
        finally
        {
            foreach (var e in McpAudit.FindAll(McpAudit._.TraceId == "test-trace-001")) e.Delete();
        }
    }

    [Fact(DisplayName = "G4: Finish() 不传 afterValue 时 AfterValue 保持为空")]
    public void Finish_WithoutAfterValue_LeavesAfterValueEmpty()
    {
        var ctx = MakeCtx();
        var tool = MakeTool().Object;

        foreach (var e in McpAudit.FindAll(McpAudit._.TraceId == "test-trace-001")) e.Delete();

        try
        {
            var audit = McpAuditWriter.Begin(ctx, tool, "{}");
            McpAuditWriter.Finish(audit, false, "boom", 1234);

            var reloaded = McpAudit.FindById(audit.Id);
            Assert.True(String.IsNullOrEmpty(reloaded.AfterValue));
        }
        finally
        {
            foreach (var e in McpAudit.FindAll(McpAudit._.TraceId == "test-trace-001")) e.Delete();
        }
    }
}
