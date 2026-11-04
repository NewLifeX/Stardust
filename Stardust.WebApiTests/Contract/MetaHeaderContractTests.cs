using System.Text.RegularExpressions;
using NewLife;
using Stardust.WebApiTests.Fixtures;
using Xunit;
using Xunit.Abstractions;

namespace Stardust.WebApiTests.Contract;

/// <summary>列表列头契约：meta 下发的列名不得残留未解析的单元格模板（如 <c>{Name}</c>）</summary>
/// <remarks>
/// 控制器习惯把单元格文字模板写在 <c>ListField.DisplayName</c>（本地化会把它当列头输出）或 <c>ListField.Text</c>。
/// 统一约定：单元格模板写 <c>Text</c>，列名由 <c>Header</c> 或实体字段描述提供；
/// 本契约遍历全部实体页面，防止 <c>{Xxx}</c> 这类模板再次漏到表头。
/// </remarks>
[Collection(WebApiCollection.Name)]
public class MetaHeaderContractTests
{
    private static readonly Regex _template = new(@"^\s*\{[^{}]+\}\s*$", RegexOptions.Compiled);

    private readonly ApiClient _api;

    public MetaHeaderContractTests(WebApiTestFactory factory, ITestOutputHelper output) => _api = new(factory, output);

    public static TheoryData<String> Endpoints => new()
    {
        "Configs/AppConfig/meta",
        "Configs/AppRule/meta",
        "Configs/ConfigData/meta",
        "Configs/ConfigHistory/meta",
        "Deployment/AppBuildNode/meta",
        "Deployment/AppDeploy/meta",
        "Deployment/AppDeployHistory/meta",
        "Deployment/AppDeployNode/meta",
        "Deployment/AppDeployVersion/meta",
        "Deployment/AppPipeline/meta",
        "Deployment/AppPipelineRun/meta",
        "Deployment/AppPipelineStep/meta",
        "Deployment/Attachment/meta",
        "Deployment/SslCertificate/meta",
        "Gateway/GatewayCert/meta",
        "Gateway/GatewayCluster/meta",
        "Gateway/GatewayNode/meta",
        "Gateway/GatewayRoute/meta",
        "Monitors/AlarmGroup/meta",
        "Monitors/AlarmHistory/meta",
        "Monitors/AlarmRecord/meta",
        "Monitors/AppDayStat/meta",
        "Monitors/AppMinuteStat/meta",
        "Monitors/AppTracer/meta",
        "Monitors/SampleData/meta",
        "Monitors/Trace/meta",
        "Monitors/TraceData/meta",
        "Monitors/TraceDayStat/meta",
        "Monitors/TraceHourStat/meta",
        "Monitors/TraceItem/meta",
        "Monitors/TraceMinuteStat/meta",
        "Monitors/TraceRule/meta",
        "MySql/MySqlData/meta",
        "MySql/MySqlNode/meta",
        "Nodes/DomainProvider/meta",
        "Nodes/DotNetPackage/meta",
        "Nodes/Node/meta",
        "Nodes/NodeCommand/meta",
        "Nodes/NodeData/meta",
        "Nodes/NodeFramework/meta",
        "Nodes/NodeHistory/meta",
        "Nodes/NodeLocation/meta",
        "Nodes/NodeOnline/meta",
        "Nodes/NodeRule/meta",
        "Nodes/NodeStat/meta",
        "Nodes/NodeVersion/meta",
        "Nodes/ProductPackage/meta",
        "Nodes/ProductRelease/meta",
        "Platform/GalaxyProject/meta",
        "Platform/McpAudit/meta",
        "Platform/McpToken/meta",
        "Platform/McpTokenResource/meta",
        "Platform/ProjectUser/meta",
        "Redis/RedisData/meta",
        "Redis/RedisMessageQueue/meta",
        "Redis/RedisNode/meta",
        "Registry/App/meta",
        "Registry/AppClientLog/meta",
        "Registry/AppCommand/meta",
        "Registry/AppConsume/meta",
        "Registry/AppHistory/meta",
        "Registry/AppMeter/meta",
        "Registry/AppOnline/meta",
        "Registry/AppService/meta",
        "Registry/Service/meta",
    };

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task ListHeaders_NotTemplate(String endpoint)
    {
        var api = await _api.GetJsonAsync(endpoint);

        Assert.True(api.Data.TryGetProperty("list", out var fields), $"[{endpoint}] meta 响应缺少 list 区段");

        foreach (var field in fields.EnumerateArray())
        {
            var name = GetString(field, "name");
            foreach (var key in new[] { "header", "displayName" })
            {
                var text = GetString(field, key);
                if (text.IsNullOrEmpty()) continue;

                Assert.False(_template.IsMatch(text), $"[{endpoint}] 字段 {name} 的列名仍是模板：{text}");
            }
        }
    }

    private static String? GetString(System.Text.Json.JsonElement field, String name)
    {
        foreach (var prop in field.EnumerateObject())
        {
            if (String.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase))
                return prop.Value.ValueKind == System.Text.Json.JsonValueKind.String ? prop.Value.GetString() : null;
        }

        return null;
    }
}
