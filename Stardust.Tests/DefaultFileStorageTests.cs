using System.ComponentModel;
using Moq;
using NewLife;
using NewLife.Caching;
using Stardust;
using Stardust.Registry;
using Stardust.Storages;
using Xunit;

namespace Stardust.Tests;

/// <summary>测试用文件存储子类。DefaultFileStorage 无抽象成员，直接继承即可</summary>
internal class TestFileStorage : DefaultFileStorage
{
    /// <summary>暴露受保护的本地文件校验方法，便于测试</summary>
    /// <param name="path">相对路径</param>
    /// <param name="hash">哈希</param>
    public new Boolean CheckLocalFile(String? path, String? hash) => base.CheckLocalFile(path, hash);
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

    #region CheckLocalFile 本地文件校验
    private static String NewDir() => Path.Combine(Path.GetTempPath(), "StarStorage_" + Guid.NewGuid().ToString("N"));

    [Fact]
    [DisplayName("CheckLocalFile_文件不存在_返回False")]
    public void CheckLocalFile_MissingFile_ReturnsFalse()
    {
        using var storage = new TestFileStorage { RootPath = NewDir() };

        Assert.False(storage.CheckLocalFile("App/x.zip", null));
    }

    [Fact]
    [DisplayName("CheckLocalFile_哈希为空且文件存在_返回True")]
    public void CheckLocalFile_NoHash_FileExists_ReturnsTrue()
    {
        var dir = NewDir();
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "a.txt"), "hello");
            using var storage = new TestFileStorage { RootPath = dir };

            Assert.True(storage.CheckLocalFile("a.txt", null));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    [DisplayName("CheckLocalFile_哈希匹配_返回True")]
    public void CheckLocalFile_HashMatch_ReturnsTrue()
    {
        var dir = NewDir();
        Directory.CreateDirectory(dir);
        try
        {
            var path = Path.Combine(dir, "a.txt");
            File.WriteAllText(path, "hello");
            var hash = path.AsFile().MD5().ToHex();
            using var storage = new TestFileStorage { RootPath = dir };

            Assert.True(storage.CheckLocalFile("a.txt", hash));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    [DisplayName("CheckLocalFile_哈希不匹配_返回False")]
    public void CheckLocalFile_HashMismatch_ReturnsFalse()
    {
        var dir = NewDir();
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "a.txt"), "hello");
            using var storage = new TestFileStorage { RootPath = dir };

            Assert.False(storage.CheckLocalFile("a.txt", "00000000000000000000000000000000"));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    [DisplayName("CheckLocalFile_文件被独占占用_容错返回False")]
    public void CheckLocalFile_FileLocked_ReturnsFalse()
    {
        var dir = NewDir();
        Directory.CreateDirectory(dir);
        try
        {
            var path = Path.Combine(dir, "a.txt");
            File.WriteAllText(path, "hello");
            var hash = path.AsFile().MD5().ToHex();

            // 以排他模式打开文件，模拟文件正被其他进程写入/替换
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                using var storage = new TestFileStorage { RootPath = dir };

                Assert.False(storage.CheckLocalFile("a.txt", hash));
            }
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }
    #endregion
}
