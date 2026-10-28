using System;
using Stardust.Data.Deployment;
using XCode;
using Xunit;

namespace ServerTest.Deployment;

/// <summary>SSL 证书实体校验测试。覆盖域名必填与正常通过路径（PUB-10）</summary>
public class SslCertificateTests
{
    [Fact(DisplayName = "域名为空时校验失败")]
    public void Valid_EmptyDomain_Throws()
    {
        var entity = new SslCertificate { Domain = "" };

        var ex = Assert.Throws<ArgumentNullException>(() => entity.Valid(DataMethod.Insert));
        Assert.Equal("Domain", ex.ParamName);
    }

    [Fact(DisplayName = "域名存在时校验通过")]
    public void Valid_Domain_Ok()
    {
        var entity = new SslCertificate
        {
            Domain = "ssl_" + Guid.NewGuid().ToString("n")[..6] + ".example.com",
            Enable = true,
        };

        Assert.True(entity.Valid(DataMethod.Insert));
    }
}
