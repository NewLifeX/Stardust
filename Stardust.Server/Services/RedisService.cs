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
                if (item.Enable) Process(item);
            }
        }

        private IDictionary<Int32, FullRedis> _servers = new Dictionary<Int32, FullRedis>();
        private void Process(RedisNode node)
        {
            if (!_servers.TryGetValue(node.Id, out var rds))
            {
                rds = new FullRedis
                {
                    Server = node.Server,
                    Password = node.Password,
                    //Log = XTrace.Log,
                };

                _servers[node.Id] = rds;
            }

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