using System;
using Stardust.Data;
using Xunit;

namespace ServerTest.Registry;

/// <summary>应用消费关系单元测试</summary>
public class AppConsumeTests
{
    [Fact]
    public void InsertAndFindById()
    {
        var entity = new AppConsume
        {
            AppId = 992001,
            ServiceId = 882001,
            ServiceName = "test-svc-" + Guid.NewGuid().ToString("n")[..8],
            Enable = true,
        };

        try
        {
            var count = entity.Insert();
            Assert.True(count > 0);
            Assert.True(entity.Id > 0);

            var found = AppConsume.FindById(entity.Id);
            Assert.NotNull(found);
            Assert.Equal(entity.AppId, found.AppId);
            Assert.Equal(entity.ServiceName, found.ServiceName);
        }
        finally
        {
            entity.Delete();
        }
    }
}
