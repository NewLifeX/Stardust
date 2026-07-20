using Stardust.Data;
using Xunit;

namespace ServerTest.Apps;

/// <summary>应用实体单元测试</summary>
public class AppEntityTests
{
    [Fact]
    public void InsertAndFindById()
    {
        var name = "test-app-" + Guid.NewGuid().ToString("n")[..8];
        var entity = new App
        {
            Name = name,
            Secret = "test-secret",
            Enable = true,
            Category = "测试",
        };

        try
        {
            var count = entity.Insert();
            Assert.True(count > 0);
            Assert.True(entity.Id > 0);

            var found = App.FindById(entity.Id);
            Assert.NotNull(found);
            Assert.Equal(name, found.Name);
            Assert.Equal("测试", found.Category);
        }
        finally
        {
            entity.Delete();
        }
    }
}
