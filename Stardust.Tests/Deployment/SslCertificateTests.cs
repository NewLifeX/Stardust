using System;
using Stardust.Data.Deployment;
using XCode;
using Xunit;

namespace Stardust.Tests.Deployment;

/// <summary>SSL 证书规则测试。覆盖证书名推导、域名匹配与文件名校验（PUB-10 / PUB-24）</summary>
public class SslCertificateTests
{
    [Theory(DisplayName = "证书名默认取域名去掉通配符")]
    [InlineData("*.newlifex.com", null, "newlifex.com")]
    [InlineData("*.feifan.link", null, "feifan.link")]
    [InlineData("sso.newlifex.com", null, "sso.newlifex.com")]
    [InlineData("*.newlifex.com", "custom", "custom")]
    public void GetCertName_DerivesFromDomain(String domain, String? certName, String expected)
    {
        var entity = new SslCertificate { Domain = domain, CertName = certName };

        Assert.Equal(expected, entity.GetCertName());
    }

    [Theory(DisplayName = "证书域名匹配支持精确与通配符")]
    [InlineData("*.newlifex.com", "sso.newlifex.com", true)]
    [InlineData("*.newlifex.com", "newlifex.com", false)]
    [InlineData("*.newlifex.com", "a.b.newlifex.com", true)]
    [InlineData("sso.newlifex.com", "sso.newlifex.com", true)]
    [InlineData("sso.newlifex.com", "www.newlifex.com", false)]
    [InlineData("a.com,b.com", "b.com", true)]
    public void MatchHost_SupportsWildcardAndMultipleDomains(String domain, String host, Boolean expected)
    {
        var entity = new SslCertificate { Domain = domain };

        Assert.Equal(expected, entity.MatchHost(host));
    }

    [Fact(DisplayName = "证书名含路径穿越字符时校验失败")]
    public void Valid_UnsafeCertName_Throws()
    {
        var entity = new SslCertificate { Domain = "*.newlifex.com", CertName = "../" };

        var ex = Assert.Throws<ArgumentException>(() => entity.Valid(DataMethod.Insert));
        Assert.Equal("CertName", ex.ParamName);
    }
}
