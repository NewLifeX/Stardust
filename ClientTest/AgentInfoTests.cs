using Stardust.Models;
using Xunit;

namespace ClientTest;

public class AgentInfoTests
{
    [Fact]
    public void Test1()
    {
        var inf = AgentInfo.GetLocal(true);

        Assert.NotNull(inf);
    }
}
