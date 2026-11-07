using System.ComponentModel;
using Moq;
using NewLife;
using NewLife.Agent;
using NewLife.Agent.WebPanel;
using NewLife.Data;
using NewLife.Http;
using NewLife.Serialization;
using StarAgent;
using StarAgent.WebPanel;
using Stardust;
using Xunit;

namespace Stardust.Tests;

/// <summary>面板测试集合。AgentWebPanel.Current 为进程级单例，禁止并行</summary>
[CollectionDefinition("StarAgentPanel", DisableParallelization = true)]
public class StarAgentPanelCollection { }

/// <summary>StarAgent 面板密钥只写不读测试</summary>
/// <remarks>
/// 面板等价于系统管理员控制台，配置页不能回显应用密钥与节点密钥；
/// 同时空值更新必须视为“保持原值”，避免前端留空保存把密钥清空。
/// </remarks>
[Collection("StarAgentPanel")]
public class StarPanelSecretTests
{
    #region 辅助
    private static (StarPanel Panel, StarApi Api) CreateApi()
    {
        var mockSvc = new Mock<ServiceBase> { CallBase = true };
        mockSvc.Object.ServiceName = "SecTest";

        var panel = new StarPanel(mockSvc.Object)
        {
            UserName = "admin",
            Password = "123456",
        };

        var token = panel.IssueToken("admin", "123456");
        var request = new HttpRequest();
        request.Parse(new ArrayPacket($"GET /star/getStarConfig HTTP/1.1\r\nHost: localhost\r\nAuthorization: Bearer {token}\r\n\r\n".GetBytes()));

        var context = new Mock<IHttpContext>();
        context.Setup(x => x.Request).Returns(request);
        context.Setup(x => x.Response).Returns(new HttpResponse());

        return (panel, new StarApi { Context = context.Object });
    }
    #endregion

    [Fact]
    [DisplayName("面板_星尘配置_不回显应用密钥与节点密钥")]
    public void GetStarConfig_MasksSecrets()
    {
        var starSet = StarSetting.Current;
        var agentSet = StarAgentSetting.Current;
        var prevStarSecret = starSet.Secret;
        var prevAgentSecret = agentSet.Secret;
        var (panel, api) = CreateApi();
        try
        {
            starSet.Secret = "star-secret-123";
            agentSet.Secret = "node-secret-456";

            var json = api.GetStarConfig().ToJson();

            Assert.DoesNotContain("star-secret-123", json);
            Assert.DoesNotContain("node-secret-456", json);
            // 字段仍保留，前端据此渲染“只写”输入框
            Assert.Contains("\"name\":\"Secret\"", json);
            Assert.Contains("\"name\":\"Secret_Agent\"", json);
        }
        finally
        {
            starSet.Secret = prevStarSecret;
            agentSet.Secret = prevAgentSecret;
            panel.Stop("TestDone");
        }
    }

    [Fact]
    [DisplayName("面板_星尘配置_空密钥不覆盖已保存密钥")]
    public void UpdateStarConfig_EmptySecret_KeepsExisting()
    {
        var starSet = StarSetting.Current;
        var agentSet = StarAgentSetting.Current;
        var prevStarSecret = starSet.Secret;
        var prevAgentSecret = agentSet.Secret;
        var prevAppKey = starSet.AppKey;
        var (panel, api) = CreateApi();
        try
        {
            starSet.Secret = "keep-star";
            agentSet.Secret = "keep-node";

            var result = api.UpdateStarConfig(new Dictionary<String, Object>
            {
                ["Secret"] = "",
                ["Secret_Agent"] = "",
                ["AppKey"] = "panel-test",
            });

            Assert.Equal(0, GetCode(result));
            Assert.Equal("keep-star", starSet.Secret);
            Assert.Equal("keep-node", agentSet.Secret);
        }
        finally
        {
            starSet.Secret = prevStarSecret;
            starSet.AppKey = prevAppKey;
            agentSet.Secret = prevAgentSecret;
            starSet.Save();
            agentSet.Save();
            panel.Stop("TestDone");
        }
    }

    private static Int32 GetCode(Object result)
    {
        using var doc = System.Text.Json.JsonDocument.Parse(result.ToJson());

        return doc.RootElement.GetProperty("code").GetInt32();
    }
}
