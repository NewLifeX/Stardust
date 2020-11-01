using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using NewLife;
using NewLife.Caching;
using NewLife.Log;
using NewLife.Threading;
using Stardust.Data.Monitors;
using Stardust.Data.Nodes;
using Stardust.DingTalk;
using Stardust.WeiXin;

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
        private ICache _cache = new MemoryCache();

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

        private IDictionary<Int32, FullRedis> _servers = new Dictionary<Int32, FullRedis>();
        private void Process(RedisNode node)
        {
            if (!_servers.TryGetValue(node.Id, out var rds))
                _servers[node.Id] = rds = new FullRedis();

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
            data.Fill(inf);
            data.Insert();
        }
    }
}