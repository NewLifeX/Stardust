using Xunit;

namespace Stardust.McpApiTests;

/// <summary>测试集合。共享一份初始化数据与令牌字典，避免每个测试类重复执行幂等初始化。</summary>
[CollectionDefinition(nameof(McpApiTestCollection))]
public class McpApiTestCollection : ICollectionFixture<McpApiTestFixture>
{
}
