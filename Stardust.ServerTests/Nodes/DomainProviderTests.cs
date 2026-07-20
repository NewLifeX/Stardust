using System;
using Stardust.Data.Nodes;
using Stardust.Services;
using Xunit;

namespace ServerTest.Nodes;

/// <summary>域名供应商管理单元测试。覆盖实体 CRUD、搜索和 IDnsConfig 接口实现</summary>
public class DomainProviderTests
{
    [Fact]
    public void Implements_IDnsConfig()
    {
        var entity = new DomainProvider();

        Assert.IsAssignableFrom<IDnsConfig>(entity);

        // 验证接口属性映射
        entity.AppKey = "test-key";
        entity.AppSecret = "test-secret";
        entity.Domain = "example.com";
        entity.Endpoint = "https://api.example.com";

        var config = (IDnsConfig)entity;
        Assert.Equal("test-key", config.AppKey);
        Assert.Equal("test-secret", config.AppSecret);
        Assert.Equal("example.com", config.Domain);
        Assert.Equal("https://api.example.com", config.Endpoint);
    }

    [Fact]
    public void Insert_ValidData_Succeeds()
    {
        var entity = new DomainProvider
        {
            Name = $"test-dp-{Guid.NewGuid():n}"[..20],
            Provider = "Aliyun",
            AppKey = "test-access-key",
            AppSecret = "test-secret-key",
            Domain = "test.newlifex.com",
            Enable = true,
        };

        try
        {
            var count = entity.Insert();

            Assert.True(count > 0);
            Assert.True(entity.Id > 0);

            // 验证持久化
            var found = DomainProvider.FindById(entity.Id);
            Assert.NotNull(found);
            Assert.Equal("Aliyun", found.Provider);
            Assert.Equal("test-access-key", found.AppKey);
            Assert.True(found.Enable);
        }
        finally
        {
            entity.Delete();
        }
    }

    [Fact]
    public void Search_ByDomain_FindsResults()
    {
        var name = $"test-dp-search-{Guid.NewGuid():n}"[..20];
        var entity = new DomainProvider
        {
            Name = name,
            Provider = "Aliyun",
            AppKey = "search-key",
            AppSecret = "search-secret",
            Domain = "search.newlifex.com",
            Enable = true,
        };

        try
        {
            entity.Insert();

            // 按域名搜索
            var list = DomainProvider.Search("search.newlifex.com", null, DateTime.MinValue, DateTime.MinValue, null, null);
            Assert.Contains(list, e => e.Name == name);

            // 按启用状态搜索
            list = DomainProvider.Search(null, true, DateTime.MinValue, DateTime.MinValue, null, null);
            Assert.Contains(list, e => e.Name == name);

            // 按禁用状态搜索，不应包含已启用的记录
            list = DomainProvider.Search(null, false, DateTime.MinValue, DateTime.MinValue, null, null);
            Assert.DoesNotContain(list, e => e.Name == name);
        }
        finally
        {
            entity.Delete();
        }
    }

    [Theory]
    [InlineData("Aliyun")]
    [InlineData("TencentCloud")]
    [InlineData("UCloud")]
    [InlineData("AWS")]
    [InlineData("Cloudflare")]
    public void Provider_CommonValues_Accepted(String provider)
    {
        var entity = new DomainProvider
        {
            Name = $"test-{provider}-{Guid.NewGuid():n}"[..20],
            Provider = provider,
            AppKey = "key",
            AppSecret = "secret",
            Domain = $"{provider.ToLower()}.test.com",
            Enable = true,
        };

        try
        {
            entity.Insert();
            Assert.True(entity.Id > 0);

            var found = DomainProvider.FindById(entity.Id);
            Assert.NotNull(found);
            Assert.Equal(provider, found.Provider);
        }
        finally
        {
            entity.Delete();
        }
    }

    [Fact]
    public void Update_ModifyCredentials_Succeeds()
    {
        var entity = new DomainProvider
        {
            Name = $"test-dp-upd-{Guid.NewGuid():n}"[..20],
            Provider = "Aliyun",
            AppKey = "original-key",
            AppSecret = "original-secret",
            Domain = "upd.test.com",
            Enable = true,
        };

        try
        {
            entity.Insert();

            // 修改凭据
            entity.AppKey = "updated-key";
            entity.AppSecret = "updated-secret";
            entity.Update();

            // 验证持久化
            var found = DomainProvider.FindById(entity.Id);
            Assert.NotNull(found);
            Assert.Equal("updated-key", found.AppKey);
            Assert.Equal("updated-secret", found.AppSecret);
        }
        finally
        {
            entity.Delete();
        }
    }
}
