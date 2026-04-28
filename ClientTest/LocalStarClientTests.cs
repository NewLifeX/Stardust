using Stardust;
using Xunit;

namespace ClientTest;

public class LocalStarClientTests
{
    [Fact]
    public void Info()
    {
        var client = new LocalStarClient();
        var inf = client.GetInfo();

        Assert.NotNull(inf);
        Assert.NotEmpty(inf.Server);
    }
}
