using Stardust.Monitors;
using Xunit;

namespace Stardust.Tests;

/// <summary>星尘追踪解析器测试。覆盖域名埋点基数控制与 URI 分段截断</summary>
public class StarTracerResolverTests
{
    [Fact]
    public void ResolveName_AbsoluteUri_ReturnsName()
    {
        // 绝对 URI 解析出埋点名称（含协议与域名前缀）
        var resolver = new StarTracerResolver();

        var name = resolver.ResolveName(new Uri("http://example.com/api/users"), null);

        Assert.False(String.IsNullOrEmpty(name));
        Assert.StartsWith("http://example.com", name);
    }

    [Fact]
    public void ResolveName_ShortSegments_Preserved()
    {
        // 短路径段（<=16字符）应保留在埋点名称中
        var resolver = new StarTracerResolver();

        var name = resolver.ResolveName(new Uri("http://example.com/api/users"), null);

        Assert.Contains("api", name);
        Assert.Contains("users", name);
    }

    [Fact]
    public void ResolveName_LongSegment_Truncated()
    {
        // 路径段超过16字符的应被截断丢弃，避免埋点名称过长
        var resolver = new StarTracerResolver();

        var name = resolver.ResolveName(new Uri("http://example.com/api/verylongsegmentname1234567890"), null);

        Assert.False(String.IsNullOrEmpty(name));
        Assert.DoesNotContain("verylongsegmentname1234567890", name);
    }

    [Fact]
    public void ResolveName_RelativeUri_QueryStripped()
    {
        // 相对 URI 去除查询参数后再解析
        var resolver = new StarTracerResolver();

        var name = resolver.ResolveName(new Uri("/api/users?id=123", UriKind.Relative), null);

        Assert.False(String.IsNullOrEmpty(name));
        Assert.DoesNotContain("?", name);
    }

    [Fact]
    public void ResolveName_HighCardinality_DegradeToHost()
    {
        // 超过 MaxTracePerHost 后降级为仅域名，防止高基数埋点爆炸
        var resolver = new StarTracerResolver { MaxTracePerHost = 5 };
        for (var i = 0; i < 5; i++)
        {
            resolver.ResolveName(new Uri($"http://example.com/api/a{i}"), null);
        }

        var degraded = resolver.ResolveName(new Uri("http://example.com/api/b0"), null);

        Assert.Equal("http://example.com", degraded);
    }

    [Fact]
    public void ResolveName_DuplicateName_NotDoubleCount()
    {
        // 同一埋点名称重复出现不应重复计数，避免过早触发高基数降级
        var resolver = new StarTracerResolver { MaxTracePerHost = 3 };
        for (var i = 0; i < 10; i++)
        {
            var name = resolver.ResolveName(new Uri("http://example.com/api/same"), null);
            Assert.NotNull(name);
        }

        // 仍应返回完整名称（计数未超限）
        var after = resolver.ResolveName(new Uri("http://example.com/api/same"), null);
        Assert.Contains("same", after);
    }
}
