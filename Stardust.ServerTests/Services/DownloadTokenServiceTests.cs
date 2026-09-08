using System;
using System.Threading;
using NewLife;
using NewLife.Caching;
using Stardust.Server.Services;
using Xunit;

namespace ServerTest.Services;

/// <summary>附件下载短时令牌服务单元测试（安全V-11）</summary>
public class DownloadTokenServiceTests
{
    private readonly DownloadTokenService _service = new(new CacheProvider());

    [Fact(DisplayName = "签发令牌后可校验通过")]
    public void Issue_Valid()
    {
        var id = DateTime.Now.Ticks % 1000000 + 1;
        var token = _service.Issue(id);

        Assert.False(token.IsNullOrEmpty());
        Assert.True(_service.Valid(id, token));
    }

    [Fact(DisplayName = "错误或空令牌校验失败")]
    public void Valid_WrongToken_Fail()
    {
        var id = DateTime.Now.Ticks % 1000000 + 2;
        _service.Issue(id);

        Assert.False(_service.Valid(id, "bad-token"));
        Assert.False(_service.Valid(id, ""));
        Assert.False(_service.Valid(id, null));
    }

    [Fact(DisplayName = "令牌不能跨附件使用")]
    public void Token_BoundToAttachment()
    {
        var id1 = DateTime.Now.Ticks % 1000000 + 3;
        var id2 = DateTime.Now.Ticks % 1000000 + 4;
        var token = _service.Issue(id1);

        Assert.False(_service.Valid(id2, token));
    }

    [Fact(DisplayName = "令牌过期后失效")]
    public void Valid_Expired_Fail()
    {
        var id = DateTime.Now.Ticks % 1000000 + 5;
        _service.Issue(id, 1);

        Thread.Sleep(1100);
        Assert.False(_service.Valid(id, "any-token"));
    }

    [Fact(DisplayName = "下载URL附加令牌且非附件链接原样返回")]
    public void AppendToken_Url()
    {
        // 附件链接：附带令牌
        var url = _service.AppendToken("/cube/file?id=12345.zip", 600);
        Assert.Contains("token=", url);

        var id = DownloadTokenService.GetAttachmentId(url);
        Assert.Equal(12345, id);

        // 非附件链接：原样返回
        var other = _service.AppendToken("http://x.newlifex.com/star/xx.zip", 600);
        Assert.Equal("http://x.newlifex.com/star/xx.zip", other);
        Assert.Equal(0, DownloadTokenService.GetAttachmentId(other));
    }

    [Fact(DisplayName = "绝对地址同样可解析并附带令牌")]
    public void AppendToken_AbsoluteUrl()
    {
        var url = _service.AppendToken("http://star.example.com/cube/file?id=987&token=old", 600);
        Assert.Contains("token=", url);
        Assert.Equal(987, DownloadTokenService.GetAttachmentId(url));
    }
}
