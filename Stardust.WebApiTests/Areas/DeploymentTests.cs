using Stardust.WebApiTests.Fixtures;
using Xunit;
using Xunit.Abstractions;

namespace Stardust.WebApiTests.Areas;

/// <summary>部署域：应用部署与部署流水线。</summary>
[Collection(WebApiCollection.Name)]
public class DeploymentTests
{
    private readonly ApiClient _api;

    public DeploymentTests(WebApiTestFactory factory, ITestOutputHelper output) => _api = new(factory, output);

    public static TheoryData<String> Endpoints => new()
    {
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
    };

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task Meta_ReturnsListFields(String endpoint)
    {
        var api = await _api.GetJsonAsync(endpoint);

        ApiAssert.MetaFields(api.Data, "list", endpoint);
    }
}
