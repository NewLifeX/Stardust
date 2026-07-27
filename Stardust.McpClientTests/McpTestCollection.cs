using Xunit;

namespace Stardust.McpClientTests;

/// <summary>测试集合。共享TestServer实例，避免每个测试类重复启动Web服务器</summary>
[CollectionDefinition(nameof(McpTestCollection))]
public class McpTestCollection : ICollectionFixture<McpTestServerFixture>
{
}
