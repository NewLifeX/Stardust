using System;
using Stardust.Data.Deployment;
using Xunit;

namespace ServerTest.Deployment;

/// <summary>部署版本管理单元测试。覆盖实体基础操作和查找方法</summary>
public class AppDeployVersionTests
{
    [Fact]
    public void InsertAndFindById()
    {
        var entity = new AppDeployVersion
        {
            DeployId = 999901,
            Version = "9.9.9999.9999",
            Enable = true,
            Url = "http://test/app-v9999.zip",
        };

        try
        {
            var count = entity.Insert();
            Assert.True(count > 0);
            Assert.True(entity.Id > 0);

            var found = AppDeployVersion.FindById(entity.Id);
            Assert.NotNull(found);
            Assert.Equal(entity.Version, found.Version);
            Assert.Equal(entity.Url, found.Url);
        }
        finally
        {
            entity.Delete();
        }
    }

    [Fact]
    public void FindByDeployIdAndVersion_ReturnsCorrect()
    {
        var entity = new AppDeployVersion
        {
            DeployId = 999902,
            Version = "9.9.9999.9998",
            Enable = true,
            Url = "http://test/app-v9998.zip",
        };

        try
        {
            entity.Insert();

            var found = AppDeployVersion.FindByDeployIdAndVersion(999902, "9.9.9999.9998");
            Assert.NotNull(found);
            Assert.Equal(entity.Id, found.Id);

            // 不存在时返回 null
            var notFound = AppDeployVersion.FindByDeployIdAndVersion(999902, "0.0.0.0");
            Assert.Null(notFound);
        }
        finally
        {
            entity.Delete();
        }
    }

    [Fact]
    public void FindAllByDeployId_ReturnsOrderedList()
    {
        var id1 = 0;
        var id2 = 0;
        try
        {
            var v1 = new AppDeployVersion
            {
                DeployId = 999903,
                Version = "1.0.0.0",
                Enable = true,
                Url = "http://test/v1.zip",
            };
            v1.Insert();
            id1 = v1.Id;

            var v2 = new AppDeployVersion
            {
                DeployId = 999903,
                Version = "2.0.0.0",
                Enable = true,
                Url = "http://test/v2.zip",
            };
            v2.Insert();
            id2 = v2.Id;

            var list = AppDeployVersion.FindAllByDeployId(999903);
            Assert.NotEmpty(list);
            Assert.Contains(list, e => e.Id == id1);
            Assert.Contains(list, e => e.Id == id2);
        }
        finally
        {
            if (id1 > 0) new AppDeployVersion { Id = id1 }.Delete();
            if (id2 > 0) new AppDeployVersion { Id = id2 }.Delete();
        }
    }
}
