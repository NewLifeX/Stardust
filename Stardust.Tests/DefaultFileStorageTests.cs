using Moq;
using NewLife.Caching;
using Stardust;
using Stardust.Registry;
using Stardust.Storages;
using Xunit;

namespace Stardust.Tests;

/// <summary>测试用文件存储子类。DefaultFileStorage 无抽象成员，直接继承即可</summary>
internal class TestFileStorage : DefaultFileStorage
{
}

/// <summary>简单服务提供者，按类型返回注册的实例</summary>
internal class FakeServiceProvider : IServiceProvider
{
    private readonly Dictionary<Type, Object?> _dic = [];

    public void Add(Type type, Object? value) => _dic[type] = value;

    public Object? GetService(Type serviceType) => _dic.TryGetValue(serviceType, out var value) ? value : null;
}

/// <summary>分布式文件存储测试。覆盖事件总线降级路径（Redis → 星尘 → MemoryCache）</summary>
public class DefaultFileStorageTests
{
    [Fact]
    public async Task InitializeAsync_NoProvider_NoThrow()
    {
        // 无服务提供者时不抛异常，事件总线保持为空
        using var storage = new TestFileStorage();

        await storage.InitializeAsync();

        Assert.Null(storage.NewFileBus);
    }

    [Fact]
    public async Task InitializeAsync_WithAppClient_UsesStarEventBus()
    {
        // IRegistry 返回 AppClient 时，应走到星尘事件总线分支（修复前为死代码）
        var provider = new FakeServiceProvider();

        // ICacheProvider 存在（非Redis），用于触发后续降级判断
        var cacheProvider = new Mock<ICacheProvider>();
        cacheProvider.SetupGet(e => e.Cache).Returns(new MemoryCache());
        cacheProvider.SetupGet(e => e.InnerCache).Returns(new MemoryCache());
        provider.Add(typeof(ICacheProvider), cacheProvider.Object);

        var client = new AppClient("http://localhost");
        provider.Add(typeof(IRegistry), client);

        using var storage = new TestFileStorage { Name = "Test", ServiceProvider = provider };

        await storage.InitializeAsync();

        Assert.NotNull(storage.NewFileBus);
        Assert.NotNull(storage.FileRequestBus);
    }

    [Fact]
    public async Task InitializeAsync_WithoutRegistry_FallbackMemoryCache()
    {
        // 无 IRegistry 时回退 MemoryCache 分支，事件总线应被创建
        var provider = new FakeServiceProvider();

        var cacheProvider = new Mock<ICacheProvider>();
        cacheProvider.SetupGet(e => e.Cache).Returns(new MemoryCache());
        cacheProvider.SetupGet(e => e.InnerCache).Returns(new MemoryCache());
        provider.Add(typeof(ICacheProvider), cacheProvider.Object);

        using var storage = new TestFileStorage { Name = "Test", ServiceProvider = provider };

        await storage.InitializeAsync();

        Assert.NotNull(storage.NewFileBus);
        Assert.NotNull(storage.FileRequestBus);
    }
}
