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
        "Deployment/AppBuildNode/GetFields?kind=List",
        "Deployment/AppDeploy/GetFields?kind=List",
        "Deployment/AppDeployHistory/GetFields?kind=List",
        "Deployment/AppDeployNode/GetFields?kind=List",
        "Deployment/AppDeployVersion/GetFields?kind=List",
        "Deployment/AppPipeline/GetFields?kind=List",
        "Deployment/AppPipelineRun/GetFields?kind=List",
        "Deployment/AppPipelineStep/GetFields?kind=List",
        "Deployment/Attachment/GetFields?kind=List",
        "Deployment/PipelineWebhook/GetFields?kind=List",
        "Deployment/SslCertificate/GetFields?kind=List",
    };

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task GetFields_ReturnsFieldList(String endpoint)
    {
        var api = await _api.GetJsonAsync(endpoint);

        ApiAssert.FieldList(api.Data, endpoint);
    }
}
