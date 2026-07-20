using System;
using Stardust.Data.Configs;
using Xunit;

namespace ServerTest.Configs;

/// <summary>配置历史单元测试</summary>
public class ConfigHistoryTests
{
    [Fact]
    public void InsertAndFindById()
    {
        var entity = new ConfigHistory
        {
            ConfigId = 993001,
            Action = "测试变更",
            Success = true,
            Remark = "单元测试创建",
        };

        try
        {
            var count = entity.Insert();
            Assert.True(count > 0);
            Assert.True(entity.Id > 0);

            var found = ConfigHistory.FindById(entity.Id);
            Assert.NotNull(found);
            Assert.Equal(993001, found.ConfigId);
            Assert.Equal("测试变更", found.Action);
            Assert.True(found.Success);
        }
        finally
        {
            entity.Delete();
        }
    }
}
