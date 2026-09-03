using Stardust.WebApiTests.Fixtures;
using Xunit;
using Xunit.Abstractions;

namespace Stardust.WebApiTests.Areas;

/// <summary>认证域：登录配置端点（匿名可访问）。</summary>
[Collection(WebApiCollection.Name)]
public class AuthTests
{
    private readonly ApiClient _api;

    public AuthTests(WebApiTestFactory factory, ITestOutputHelper output) => _api = new(factory, output);

    [Fact]
    public async Task LoginConfig_ReturnsOk()
    {
        var resp = await _api.GetAsync("Auth/LoginConfig", auth: false);

        Assert.Equal(System.Net.HttpStatusCode.OK, resp.StatusCode);
    }
}
