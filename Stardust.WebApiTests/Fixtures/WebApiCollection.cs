using Xunit;

namespace Stardust.WebApiTests.Fixtures;

/// <summary>
/// 共享测试集合：所有 WebApi 集成测试共用同一个 <see cref="WebApiTestFactory"/> 实例，
/// 避免每个测试类各自启动一个完整 ASP.NET Core 主机导致内存占用过高（OOM）。
/// </summary>
[CollectionDefinition(Name)]
public class WebApiCollection : ICollectionFixture<WebApiTestFactory>
{
    /// <summary>集合名。测试类用 [Collection(WebApiCollection.Name)] 声明，避免各处写字符串字面量。</summary>
    public const String Name = "WebApi";
}
