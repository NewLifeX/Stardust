using Stardust.Services;
using Xunit;

namespace Stardust.Tests;

/// <summary>星尘事件总线工厂测试。覆盖统一创建入口的降级行为（通道降级链由 DefaultFileStorageTests 覆盖）</summary>
public class StarEventBusFactoryTests
{
    [Fact(DisplayName = "无服务提供者时返回null且不抛异常")]
    public void CreateEventBus_NullProvider_ReturnsNull()
    {
        var factory = new StarEventBusFactory(null);

        Assert.Null(factory.CreateEventBus<Int32>("topic"));
    }
}
