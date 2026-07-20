using Stardust.Data.Platform;
using Xunit;

namespace ServerTest.Platform;

/// <summary>星系项目管理单元测试</summary>
public class GalaxyProjectTests
{
    [Fact]
    public void InsertAndFindById()
    {
        var entity = new GalaxyProject
        {
            Name = "test-project-" + Guid.NewGuid().ToString("n")[..8],
            Enable = true,
        };

        try
        {
            var count = entity.Insert();
            Assert.True(count > 0);
            Assert.True(entity.Id > 0);

            var found = GalaxyProject.FindById(entity.Id);
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
