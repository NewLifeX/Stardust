using Stardust.WebApiTests.Fixtures;
using Xunit;
using Xunit.Abstractions;

namespace Stardust.WebApiTests.Contract;

/// <summary>
/// 统一响应契约测试：验证 Cube 的 <c>{code,data}</c> 信封在所有 ViewKinds 下都成立。
/// 各区域端点的具体覆盖见 <c>Areas/</c> 下按区域组织的文件。
/// </summary>
[Collection(WebApiCollection.Name)]
public class ApiResponseContractTests
{
    private readonly ApiClient _api;

    public ApiResponseContractTests(WebApiTestFactory factory, ITestOutputHelper output) => _api = new(factory, output);

    /// <summary>GetFields 支持的全部视图类型，见 NewLife.Cube.ViewModels.ViewKinds。</summary>
    public static TheoryData<String> ViewKinds => new()
    {
        "List",
        "Detail",
        "AddForm",
        "EditForm",
        "Search",
    };

    [Theory]
    [MemberData(nameof(ViewKinds))]
    public async Task GetFields_AllViewKinds_ReturnsSameEnvelope(String kind)
    {
        var endpoint = $"Configs/AppConfig/GetFields?kind={kind}";

        var api = await _api.GetJsonAsync(endpoint);

        ApiAssert.FieldList(api.Data, endpoint);
    }
}
