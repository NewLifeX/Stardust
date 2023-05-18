using System;
using NewLife;
using NewLife.Caching;
using NewLife.Configuration;
using NewLife.Log;
using NewLife.Model;
using Stardust.Services;

namespace Stardust.Extensions.Caches;

/// <summary>Redis缓存服务。由Redis提供标准缓存和队列服务</summary>
public class RedisCacheService : CacheService
{
    #region 属性
    private FullRedis _redis;
    private FullRedis _redisQueue;

    /// <summary>队列</summary>
    public FullRedis RedisQueue => _redisQueue;
    #endregion

    #region 构造
    /// <summary>实例化Redis缓存服务，自动创建FullRedis对象</summary>
    /// <param name="serviceProvider"></param>
    public RedisCacheService(IServiceProvider serviceProvider)
    {
        var config = serviceProvider?.GetService<IConfigProvider>();
        if (config != null)
        {
            var cacheConn = config["RedisCache"];
            var queueConn = config["RedisQueue"];

            // 实例化全局缓存和队列，如果未设置队列，则使用缓存对象
            if (!cacheConn.IsNullOrEmpty())
            {
                //_redis = new FullRedis
                //{
                //    Name = "Cache",
                //    Tracer = serviceProvider.GetService<ITracer>()
                //};
                //_redis.Init(cacheConn);
                _redis = new FullRedis(serviceProvider, "RedisCache")
                {
                    Log = serviceProvider.GetService<ILog>()
                };

                _redisQueue = _redis;
                Cache = _redis;
            }
            if (!queueConn.IsNullOrEmpty())
            {
                _redisQueue = new FullRedis(serviceProvider, "RedisQueue")
                {
                    Log = serviceProvider.GetService<ILog>()
                };
            }
        }
    }
    #endregion

    #region 方法
    /// <summary>获取队列。各功能模块跨进程共用的队列，默认使用LIST，带消费组时使用STREAM</summary>
    /// <typeparam name="T">消息类型</typeparam>
    /// <param name="topic">主题</param>
    /// <param name="group">消费组。指定消费组时，使用STREAM</param>
    /// <returns></returns>
    public override IProducerConsumer<T> GetQueue<T>(String topic, String group = null)
    {
        if (_redisQueue != null)
        {
            if (group.IsNullOrEmpty()) return _redisQueue.GetQueue<T>(topic);

            var rs = _redisQueue.GetStream<T>(topic);
            rs.Group = group;

            XTrace.WriteLine("[{0}]队列消息数：{1}", topic, rs.Count);

            return rs;
        }

        return base.GetQueue<T>(topic, group);
    }
    #endregion
}
