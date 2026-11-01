using Stardust.Server.Services;
using Xunit;

namespace ServerTest.Services;

/// <summary>下载地址补全与加签（DownloadSourceHelper.Absolutize）测试。
/// 语义：相对路径按服务端基础地址补全为绝对地址后统一加签；
/// 已是绝对地址的只加签；空地址原样返回不加签。</summary>
public class DownloadSourceHelperTests
{
    private const String BaseUri = "https://star.example.com:6600";

    [Fact]
    [Trait("Category", "Nodes")]
    public void RelativeSource_IsAbsolutizedThenSigned()
    {
        var signed = new List<String>();

        var result = DownloadSourceHelper.Absolutize("/files/staragent.tar.gz", BaseUri, source =>
        {
            signed.Add(source);
            return source + "?token=abc";
        });

        Assert.Equal("https://star.example.com:6600/files/staragent.tar.gz?token=abc", result);
        Assert.Single(signed);
        Assert.Equal("https://star.example.com:6600/files/staragent.tar.gz", signed[0]);
    }

    /// <summary>绝对地址不再拼接基础地址，但仍需加签。</summary>
    [Fact]
    [Trait("Category", "Nodes")]
    public void AbsoluteSource_IsSignedWithoutRewriting()
    {
        var signed = new List<String>();

        var result = DownloadSourceHelper.Absolutize("http://cdn.example.com/pkg.zip", BaseUri, source =>
        {
            signed.Add(source);
            return source + "?token=abc";
        });

        Assert.Equal("http://cdn.example.com/pkg.zip?token=abc", result);
        Assert.Single(signed);
        Assert.Equal("http://cdn.example.com/pkg.zip", signed[0]);
    }

    /// <summary>空地址直接返回，不调用加签，避免产出 "?token=" 这类无意义地址。</summary>
    [Theory]
    [Trait("Category", "Nodes")]
    [InlineData(null)]
    [InlineData("")]
    public void EmptySource_IsReturnedUnchanged(String? source)
    {
        var called = false;

        var result = DownloadSourceHelper.Absolutize(source, BaseUri, value =>
        {
            called = true;
            return value;
        });

        Assert.Equal(source, result);
        Assert.False(called);
    }

    /// <summary>带路径前缀的服务端地址（如反向代理挂在子路径）也要正确拼接。</summary>
    [Fact]
    [Trait("Category", "Nodes")]
    public void BaseUriWithPathPrefix_IsRespected()
    {
        var result = DownloadSourceHelper.Absolutize(
            "/files/pkg.zip",
            "https://star.example.com:6600/agent",
            source => source);

        Assert.Equal("https://star.example.com:6600/files/pkg.zip", result);
    }
}
