using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using NewLife;
using NewLife.Caching;
using NewLife.Log;
using NewLife.Threading;
using Stardust.Data.Nodes;

namespace Stardust.Server.Services
{
    public interface IRedisService
    {
    }

    public class RedisService : BackgroundService, IRedisService
    {
        /// <summary>计算周期。默认30秒</summary>
        public Int32 Period { get; set; } = 30;

        private TimerX _timer;
        private readonly ICache _cache = new MemoryCache();

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // 初始化定时器
            _timer = new TimerX(DoWork, null, 5_000, Period * 1000) { Async = true };

            return Task.CompletedTask;
        }

        private void DoWork(Object state)
        {
            var list = RedisNode.FindAllWithCache();
            foreach (var item in list)
            {
                if (item.Enable)
                {
                    // 捕获异常，不要影响后续操作
                    var key = $"redisService:error:{item.Id}";
                    var errors = _cache.Get<Int64>(key);
                    if (errors < 5)
                    {
                        try
                        {
                            Process(item);

                            _cache.Remove(key);
                        }
                        catch
                        {
                            errors = _cache.Increment(key, 1);
                            if (errors <= 1)
                                _cache.SetExpire(key, TimeSpan.FromMinutes(10));
                        }
                    }
                    else
                    {
                        item.Enable = false;
                        item.SaveAsync();
                    }
                }
            }
        }

        private readonly IDictionary<Int32, FullRedis> _servers = new Dictionary<Int32, FullRedis>();
        private readonly IDictionary<String, FullRedis> _servers2 = new Dictionary<String, FullRedis>();
        private void Process(RedisNode node)
        {
            if (!_servers.TryGetValue(node.Id, out var rds)) _servers[node.Id] = rds = new FullRedis();

            // 可能后面更新了服务器地址和密码
            rds.Server = node.Server;
            rds.Password = node.Password;

            //var inf = rds.GetInfo(true);
            var inf = rds.GetInfo(false);
            node.Fill(inf);
            node.SaveAsync();

            var data = new RedisData
            {
                RedisId = node.Id,
                Name = node.Name,
            };
            var dbs = data.Fill(inf);
            data.Insert();

            // 扫描队列
            if (node.ScanQueue && dbs != null) ScanQueue(node, dbs);
        }

        private void ScanQueue(RedisNode node, RedisData.KeyEntry[] dbs)
        {
            var queues = RedisMessageQueue.FindAllByRedisId(node.Id);

            for (var i = 0; i < dbs.Length; i++)
            {
                if (dbs[i] == null) continue;

                var key = $"{node.Id}-{i}";
                if (!_servers2.TryGetValue(key, out var rds2)) _servers2[key] = rds2 = new FullRedis();

                rds2.Server = node.Server;
                rds2.Password = node.Password;
                rds2.Db = i;

                // keys个数太大不支持扫描
                if (rds2.Count < 10000)
                {
                    foreach (var item in rds2.Search("*:Status:*", 1000))
                    {
                        var ss = item.Split(":");
                        var topic = ss.Take(ss.Length - 2).Join(":");

                        var mq = queues.FirstOrDefault(e => e.Db == i && e.Topic == topic);
                        if (mq == null)
                        {
                            mq = new RedisMessageQueue
                            {
                                RedisId = node.Id,
                                Db = i,
                                Topic = topic,
                                Enable = true,
                            };

                            queues.Add(mq);
                        }

                        if (mq.Name.IsNullOrEmpty()) mq.Name = node.Name;
                        if (mq.Category.IsNullOrEmpty()) mq.Category = node.Category;

                        mq.SaveAsync();
                    }
                }
            }
        }
    }
}