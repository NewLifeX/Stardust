using NewLife;
using Stardust.WebApiTests.Fixtures;
using Xunit;
using Xunit.Abstractions;

namespace Stardust.WebApiTests.Areas;

/// <summary>平台域：星图项目与 MCP。</summary>
[Collection(WebApiCollection.Name)]
public class PlatformTests
{
    private readonly ApiClient _api;

    public PlatformTests(WebApiTestFactory factory, ITestOutputHelper output) => _api = new(factory, output);

    public static TheoryData<String> Endpoints => new()
    {
        "Platform/GalaxyProject/meta",
        "Platform/McpAudit/meta",
        "Platform/McpToken/meta",
        "Platform/McpTokenResource/meta",
        "Platform/ProjectUser/meta",
    };

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task Meta_ReturnsListFields(String endpoint)
    {
        var api = await _api.GetJsonAsync(endpoint);

        ApiAssert.MetaFields(api.Data, "list", endpoint);
    }

    /// <summary>配置页（ConfigController）没有列表区段，字段统一在 form 区段下发。</summary>
    public static TheoryData<String> ConfigPages => new()
    {
        "Platform/StarServer/meta",
    };

    [Theory]
    [MemberData(nameof(ConfigPages))]
    public async Task Meta_ConfigPage_ReturnsFormFields(String endpoint)
    {
        var api = await _api.GetJsonAsync(endpoint);

        ApiAssert.MetaFields(api.Data, "form", endpoint);
    }

    [Theory(DisplayName = "配置页字段按请求语言本地化，界面语言不再由平台设置提供")]
    [InlineData("zh-Hans", "服务端口")]
    [InlineData("en", "Service port")]
    public async Task Meta_ConfigPage_LocalizesFields(String lang, String portDisplayName)
    {
        var api = await _api.GetJsonAsync($"Platform/StarServer/meta?lang={lang}");
        var form = api.Data.GetProperty("form");

        var fields = new Dictionary<String, String?>(StringComparer.OrdinalIgnoreCase);
        foreach (var field in form.EnumerateArray())
        {
            var name = GetProperty(field, "Name");
            if (!name.IsNullOrEmpty()) fields[name!] = GetProperty(field, "DisplayName");
        }

        // Web/WebApi 通过 Compile Include 复用 Stardust.Server 源码，配置项描述必须能按请求语言取到译文
        Assert.Equal(portDisplayName, fields["Port"]);

        // 界面语言统一由核心库基础配置提供，平台设置不再暴露该配置项
        Assert.False(fields.ContainsKey("Language"));
    }

    private static String? GetProperty(System.Text.Json.JsonElement element, String name)
    {
        foreach (var prop in element.EnumerateObject())
        {
            if (String.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase)) return prop.Value.GetString();
        }

        return null;
    }
}
