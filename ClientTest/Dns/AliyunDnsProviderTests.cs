using NewLife.Log;
using Stardust.Dns;
using Stardust.Services;
using Xunit;

namespace ClientTest.Dns;

/// <summary>阿里云DNS供应商测试。覆盖供应商标识、默认参数、自定义端点与响应解析（全部离线可测）</summary>
public class AliyunDnsProviderTests
{
    [Fact]
    [Trait("Category", "AliyunDnsProvider")]
    public void ProviderType_IsAliyun()
    {
        var provider = new AliyunDnsProvider();

        Assert.Equal("Aliyun", provider.ProviderType);
    }

    [Fact]
    [Trait("Category", "AliyunDnsProvider")]
    public void Defaults_EndpointAndApiVersion()
    {
        var provider = new AliyunDnsProvider();

        Assert.Equal("https://alidns.aliyuncs.com/", provider.Endpoint);
        Assert.Equal("2015-01-09", provider.ApiVersion);
    }

    [Fact]
    [Trait("Category", "AliyunDnsProvider")]
    public async Task UpdateRecord_MissingCredentials_ReturnsFalseWithoutNetwork()
    {
        var provider = new AliyunDnsProvider { Log = Logger.Null };

        Assert.False(await provider.UpdateRecordAsync(new FakeDnsConfig { Domain = "newlifex.com" }, "sh05.newlifex.com", "127.0.0.1"));
        Assert.False(await provider.UpdateRecordAsync(new FakeDnsConfig { AppKey = "key" }, "sh05.newlifex.com", "127.0.0.1"));
        Assert.False(await provider.UpdateRecordAsync(new FakeDnsConfig { AppSecret = "secret" }, "sh05.newlifex.com", "127.0.0.1"));
    }

    [Fact]
    [Trait("Category", "AliyunDnsProvider")]
    public async Task UpdateRecord_CustomEndpoint_TakenFromConfig()
    {
        var provider = new AliyunDnsProvider { Log = Logger.Null };
        // 指向本机未监听端口，请求必然失败；此处只验证配置里的端点被采纳
        var config = new FakeDnsConfig { AppKey = "key", AppSecret = "secret", Endpoint = "http://127.0.0.1:1/" };

        Assert.False(await provider.UpdateRecordAsync(config, "sh05.newlifex.com", "127.0.0.1"));
        Assert.Equal("http://127.0.0.1:1/", provider.Endpoint);
    }

    [Fact]
    [Trait("Category", "AliyunDnsProvider")]
    public void Parse_DescribeDomainRecords_ReadsRecords()
    {
        const String json = """{"RequestId":"req-1","TotalCount":1,"DomainRecords":{"Record":[{"RecordId":"123","RR":"sh05","Type":"A","Value":"1.2.3.4","DomainName":"newlifex.com","TTL":600}]}}""";

        var rs = AliyunDnsResponse.Parse(json);

        Assert.NotNull(rs);
        Assert.Equal("req-1", rs!.RequestId);
        Assert.NotNull(rs.Records);
        Assert.Single(rs.Records!);
        Assert.Equal("123", rs.Records![0].RecordId);
        Assert.Equal("sh05", rs.Records![0].RR);
        Assert.Equal("A", rs.Records![0].Type);
        Assert.Equal("1.2.3.4", rs.Records![0].Value);
        Assert.False(rs.HasError);
    }

    [Fact]
    [Trait("Category", "AliyunDnsProvider")]
    public void Parse_AddDomainRecord_ReadsRecordId()
    {
        const String json = """{"RequestId":"req-2","RecordId":"456"}""";

        var rs = AliyunDnsResponse.Parse(json);

        Assert.NotNull(rs);
        Assert.Equal("456", rs!.RecordId);
        Assert.Null(rs.Records);
        Assert.False(rs.HasError);
    }

    [Fact]
    [Trait("Category", "AliyunDnsProvider")]
    public void Parse_EmptyJson_ReturnsNull()
    {
        Assert.Null(AliyunDnsResponse.Parse(""));
    }

    [Fact]
    [Trait("Category", "AliyunDnsProvider")]
    public void HasError_TrueWhenNoRequestIdRecordIdAndRecords()
    {
        var rs = new AliyunDnsResponse();

        Assert.True(rs.HasError);
    }

    [Fact]
    [Trait("Category", "AliyunDnsProvider")]
    public void RecordInfo_Parse_NullDict_ReturnsNull()
    {
        Assert.Null(AliyunDnsRecordInfo.Parse(null));
    }

    private sealed class FakeDnsConfig : IDnsConfig
    {
        public String? AppKey { get; set; }

        public String? AppSecret { get; set; }

        public String? Domain { get; set; }

        public String? Endpoint { get; set; }
    }
}
