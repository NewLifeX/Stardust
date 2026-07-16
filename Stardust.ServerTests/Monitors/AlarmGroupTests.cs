using Stardust.Data.Monitors;
using Xunit;

namespace ServerTest.Monitors;

/// <summary>告警组单元测试</summary>
public class AlarmGroupTests
{
    [Fact]
    public void InsertAndFindById()
    {
        var entity = new AlarmGroup
        {
            Name = "test-alarm-" + Guid.NewGuid().ToString("n")[..8],
            WebHook = "https://oapi.dingtalk.com/robot/test",
            Enable = true,
        };

        try
        {
            var count = entity.Insert();
            Assert.True(count > 0);
            Assert.True(entity.Id > 0);

            var found = AlarmGroup.FindById(entity.Id);
            Assert.NotNull(found);
            Assert.Equal(entity.Name, found.Name);
            Assert.True(found.Enable);
        }
        finally
        {
            entity.Delete();
        }
    }
}
