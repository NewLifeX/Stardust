using Stardust.Data;
using Xunit;

namespace ServerTest.Apps;

/// <summary>应用客户端日志单元测试</summary>
public class AppClientLogTests
{
    [Fact]
    public void InsertAndFindById()
    {
        var entity = new AppClientLog
        {
            AppId = 995001,
            ClientId = "test-client-" + Guid.NewGuid().ToString("n")[..8],
            Kind = "info",
            Message = "单元测试日志",
        };

        try
        {
            var count = entity.Insert();
            Assert.True(count > 0);
            Assert.True(entity.Id > 0);

            var found = AppClientLog.FindById(entity.Id);
            Assert.NotNull(found);
            Assert.Equal(995001, found.AppId);
            Assert.Equal(entity.ClientId, found.ClientId);
            Assert.Equal("info", found.Kind);
        }
        finally
        {
            entity.Delete();
        }
    }
}
