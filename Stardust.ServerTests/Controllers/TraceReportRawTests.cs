using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using NewLife;
using NewLife.Remoting;
using NewLife.Remoting.Models;
using NewLife.Security;
using Stardust.Data;
using Stardust.Data.Monitors;
using Stardust.Server;
using Xunit;

namespace ServerTest.Controllers;

/// <summary>压缩埋点上报测试。覆盖 Trace/ReportRaw 的 JSON 与 GZip 两条读体路径，以及鉴权要求</summary>
[Collection(StarTestServerCollection.Name)]
public class TraceReportRawTests : StarTestBase
{
    public TraceReportRawTests(StarTestServer fixture) : base(fixture) { }

    private async Task<String> AppLoginTokenAsync(String appId, String secret)
    {
        var client = _server.CreateClient();
        var rs = await client.PostAsync<LoginResponse>("app/login", new { AppId = appId, Secret = secret });
        Assert.NotNull(rs?.Token);
        return rs.Token;
    }

    private static App CreateApp(String name, String secret)
    {
        var app = new App { Name = name, Secret = secret, Enable = true };
        app.Insert();

        // 埋点上报需要已存在的 AppTracer
        new AppTracer { Name = name, AppId = app.Id, Enable = true }.Insert();

        return app;
    }

    private async Task<String> ReportRawAsync(String appId, String token, Boolean gzip)
    {
        var json = $@"{{""AppId"":""{appId}"",""ClientId"":""raw-client""}}";

        HttpContent content;
        if (gzip)
        {
            var ms = new MemoryStream();
            using (var gs = new GZipStream(ms, CompressionMode.Compress, true))
            {
                var buf = json.GetBytes();
                gs.Write(buf, 0, buf.Length);
            }
            content = new ByteArrayContent(ms.ToArray());
            content.Headers.ContentType = new("application/x-gzip");
        }
        else
        {
            content = new StringContent(json, Encoding.UTF8, "application/json");
        }

        var client = _server.CreateClient();
        if (!token.IsNullOrEmpty()) client.DefaultRequestHeaders.Add("X-Token", token);

        var response = await client.PostAsync("/Trace/ReportRaw", content);
        return await response.Content.ReadAsStringAsync();
    }

    [Fact(DisplayName = "ReportRaw 接收明文 JSON 并返回采样策略")]
    public async Task ReportRaw_JsonBody_ReturnsPeriod()
    {
        var app = CreateApp("trw_" + Rand.NextString(8), "trw-secret");
        try
        {
            var token = await AppLoginTokenAsync(app.Name, "trw-secret");

            var body = await ReportRawAsync(app.Name, token, false);

            Assert.Contains("period", body, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("匿名", body);
        }
        finally
        {
            AppTracer.FindByName(app.Name)?.Delete();
            app.Delete();
        }
    }

    [Fact(DisplayName = "ReportRaw 接收 GZip 压缩体并解压处理")]
    public async Task ReportRaw_GzipBody_ReturnsPeriod()
    {
        var app = CreateApp("trg_" + Rand.NextString(8), "trg-secret");
        try
        {
            var token = await AppLoginTokenAsync(app.Name, "trg-secret");

            var body = await ReportRawAsync(app.Name, token, true);

            Assert.Contains("period", body, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("匿名", body);
        }
        finally
        {
            AppTracer.FindByName(app.Name)?.Delete();
            app.Delete();
        }
    }

    [Fact(DisplayName = "ReportRaw 未携带令牌时被拒绝")]
    public async Task ReportRaw_WithoutToken_Rejected()
    {
        var app = CreateApp("trn_" + Rand.NextString(8), "trn-secret");
        try
        {
            var body = await ReportRawAsync(app.Name, "", false);

            Assert.Contains("匿名", body);
        }
        finally
        {
            AppTracer.FindByName(app.Name)?.Delete();
            app.Delete();
        }
    }
}
