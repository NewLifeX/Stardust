using NewLife;
using NewLife.Caching;
using NewLife.Messaging;
using NewLife.Model;
using NewLife.Reflection;
using Stardust.Registry;

namespace Stardust.Services;

/// <summary>星尘事件总线工厂。统一创建事件总线，自动选择合适的通信通道</summary>
/// <remarks>
/// 通道降级策略与文件共享保持一致：
/// 优先 Redis 队列（要求 FullRedis 且版本≥5.0），其次星尘 WebSocket 通道，最后内存缓存（同进程场景）。
/// 业务代码只管创建和收发事件，无需感知底层通道。没有任何可用通道时返回null。
/// </remarks>
/// <param name="provider">服务提供者，用于解析缓存提供者与注册中心</param>
public class StarEventBusFactory(IServiceProvider? provider) : IEventBusFactory
{
    /// <summary>创建事件总线。统一降级：优先Redis队列，其次星尘WebSocket，最后内存缓存；均不可用时返回null</summary>
    /// <typeparam name="TEvent">事件类型</typeparam>
    /// <param name="topic">事件主题</param>
    /// <param name="clientId">客户端标识。为空时使用当前节点标识（Redis作为消费组）</param>
    /// <returns>事件总线实例</returns>
    public IEventBus<TEvent>? CreateEventBus<TEvent>(String topic, String clientId = "")
    {
        if (topic.IsNullOrEmpty()) return null;

        var cid = clientId.IsNullOrEmpty() ? Runtime.ClientId : clientId;
        var cacheProvider = provider.GetService<ICacheProvider>();

        // 尝试使用Redis（版本 >= 5.0）
        if (cacheProvider != null)
        {
            var queue = cacheProvider.GetValue("RedisQueue", false) as ICache ?? cacheProvider.Cache;
            if (queue is Cache cache)
            {
                // 旧版本的Redis不支持Stream，无法用作事件总线
                var rtype = Type.GetType("NewLife.Caching.FullRedis, NewLife.Redis");
                if (rtype != null && rtype.IsAssignableFrom(queue.GetType()) &&
                    (queue.GetValue("Version") is Version ver && ver >= new Version(5, 0)))
                {
                    var bus = cache.CreateEventBus<TEvent>(topic, cid);
                    if (bus != null) return bus;
                }
            }
        }

        // Redis失败，尝试使用星尘事件总线
        if (provider.GetService<IRegistry>() is AppClient client)
        {
            var bus = client.CreateEventBus<TEvent>(topic);
            if (bus != null) return bus;
        }

        // 星尘事件总线失败，尝试使用MemoryCache
        if (cacheProvider?.Cache is MemoryCache mem)
        {
            var bus = mem.CreateEventBus<TEvent>(topic, cid);
            if (bus != null) return bus;
        }

        return null;
    }
}
