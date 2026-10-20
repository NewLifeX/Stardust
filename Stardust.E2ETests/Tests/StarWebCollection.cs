using Stardust.E2ETests.Fixtures;
using Xunit;

namespace Stardust.E2ETests.Tests;

/// <summary>StarWeb 端到端测试集合：共享同一个站点实例与浏览器会话</summary>
[CollectionDefinition(Name)]
public class StarWebCollection : ICollectionFixture<StarWebAppFixture>
{
    /// <summary>集合名</summary>
    public const String Name = "StarWeb";
}
