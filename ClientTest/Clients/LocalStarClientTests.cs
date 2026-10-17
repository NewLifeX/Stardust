using Stardust;
using Xunit;

namespace ClientTest.Clients;

public class LocalStarClientTests
{
    [Fact]
    public void Info()
    {
        var client = new LocalStarClient();
        var inf = client.GetInfo();

        // CI 无本机 StarAgent 时 GetInfo 返回空，跳过本地探测相关断言
        if (inf == null) return;
        Assert.NotEmpty(inf.Server);
    }
}
