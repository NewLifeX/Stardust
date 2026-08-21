using Xunit;

namespace ServerTest.Web;

public class WebStartupServiceRegistrationTests
{
    private static String GetSourcePath(String relativePath) =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../", relativePath));

    [Fact]
    public void Startup_Registers_HttpClientFactory_And_MySqlService()
    {
        var source = File.ReadAllText(GetSourcePath("Stardust.Web/Startup.cs"));

        Assert.Contains("services.AddHttpClient();", source);
        Assert.Contains("services.AddSingleton<IMySqlService, MySqlService>();", source);
        Assert.Contains("services.AddHostedService(s => (MySqlService)s.GetRequiredService<IMySqlService>());", source);
    }

    [Fact]
    public void AppClientLogController_Uses_ShardingAware_Search()
    {
        var source = File.ReadAllText(GetSourcePath("Stardust.Web/Areas/Registry/Controllers/AppClientLogController.cs"));

        Assert.Contains("var appId = p[\"appId\"].ToInt(-1);", source);
        Assert.Contains("var threadId = p[\"threadId\"];", source);
        Assert.Contains("if (start.Year < 2000 && end.Year < 2000)", source);
        Assert.Contains("return AppClientLog.Search(threadId, appId, start, end, p[\"Q\"], p);", source);
    }
}
