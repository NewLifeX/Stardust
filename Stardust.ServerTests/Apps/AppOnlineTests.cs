using Stardust.Data;
using XCode;
using Xunit;

namespace ServerTest.Apps;

/// <summary>应用在线实体单元测试</summary>
public class AppOnlineTests
{
    [Fact(DisplayName = "超长Macs在Valid时被截断为字段长度，不再抛长度超限异常")]
    public void Valid_ExtraLongMacs_Trimmed()
    {
        // 模拟 StarAgent 上报大量 Docker/虚拟网卡地址，远超 Macs 字段 200 字符限制
        var macs = String.Join(",", Enumerable.Range(0, 60).Select(e => $"02-42-06-A2-5A-{e:D2}"));
        Assert.True(macs.Length > 200);

        var entity = new AppOnline
        {
            AppId = 1,
            Client = "127.0.0.1@1",
            Macs = macs,
        };

        // 生产回归：Valid 内部先把超长字段截断，再校验字段长度，不应抛出 ArgumentOutOfRangeException
        entity.Valid(DataMethod.Update);

        Assert.Equal(200, entity.Macs.Length);
        Assert.Equal(macs[..200], entity.Macs);
    }
}
