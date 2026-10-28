using Stardust.WebApiTests.Fixtures;
using Xunit;
using Xunit.Abstractions;

namespace Stardust.WebApiTests.Contract;

/// <summary>
/// 统一响应契约测试：验证 Cube 的 <c>{code,data}</c> 信封在 <c>meta</c> 端点的各字段区段下都成立。
/// 各区域端点的具体覆盖见 <c>Areas/</c> 下按区域组织的文件。
/// </summary>
[Collection(WebApiCollection.Name)]
public class ApiResponseContractTests
{
    private readonly ApiClient _api;

    public ApiResponseContractTests(WebApiTestFactory factory, ITestOutputHelper output) => _api = new(factory, output);

    /// <summary>meta 标准端点的字段区段：list（列表）、form（新增/编辑表单）、search（搜索）。</summary>
    public static TheoryData<String> Sections => new()
    {
        "list",
        "form",
        "search",
    };

    [Theory]
    [MemberData(nameof(Sections))]
    public async Task GetMeta_AllSections_ReturnsSameEnvelope(String section)
    {
        var endpoint = "Configs/AppConfig/meta";

        var api = await _api.GetJsonAsync(endpoint);

        ApiAssert.MetaFields(api.Data, section, endpoint);
    }

    /// <summary>add / detail 与 form 一致时被省略，出现时必须是合法字段数组。</summary>
    [Theory]
    [InlineData("add")]
    [InlineData("detail")]
    public async Task GetMeta_OptionalSections_AreValidWhenPresent(String section)
    {
        var endpoint = "Configs/AppConfig/meta";

        var api = await _api.GetJsonAsync(endpoint);

        ApiAssert.MetaOptionalFields(api.Data, section, endpoint);
    }
}
