using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using NewLife;
using NewLife.Caching;
using NewLife.Log;
using NewLife.Serialization;
using NewLife.Threading;
using Stardust.Data.Models;
using Stardust.Data.Nodes;

namespace Stardust.Server.Services
{
    public interface IRedisService
    {
        void TraceNode(RedisNode node);

        void TraceQueue(RedisMessageQueue queue);
    }

    public class RedisService : IHostedService, IRedisService
    {
        /// <summary>计算周期。默认60秒</summary>
        public Int32 Period { get; set; } = 60;

        private TimerX _traceNode;
        private TimerX _traceQueue;
        private readonly ICache _cache = new MemoryCache();
        private readonly ITracer _tracer;

        public RedisService(ITracer tracer) => _tracer = tracer;

        public Task StartAsync(CancellationToken cancellationToken)
        {
            // 初始化定时器
            _traceNode = new TimerX(DoTraceNode, null, 5_000, Period * 1000) { Async = true };
            _traceQueue = new TimerX(DoTraceQueue, null, 10_000, Period * 1000) { Async = true };

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _traceNode?.TryDispose();
            _traceQueue?.TryDispose();

            return Task.CompletedTask;
        }

        private void DoTraceNode(Object state)
        {
            var list = RedisNode.FindAllWithCache();
            foreach (var item in list)
            {
                if (item.Enable)
                {
                    // 捕获异常，不要影响后续操作
                    var key = $"DoTraceNode:{item.Id}";
                    var errors = _cache.Get<Int64>(key);
                    if (errors < 5)
                    {
                        try
                        {
                            TraceNode(item);

                            _cache.Remove(key);
                        }
                        catch (Exception ex)
                        {
                            errors = _cache.Increment(key, 1);
                            if (errors <= 1)
                                _cache.SetExpire(key, TimeSpan.FromMinutes(10));

                            XTrace.WriteException(ex);
                        }
                    }
                    else
                    {
                        item.Enable = false;
                        item.SaveAsync();

                        _cache.Remove(key);
                    }
                }
            }
        }

        private readonly IDictionary<Int32, FullRedis> _servers = new Dictionary<Int32, FullRedis>();
        private readonly IDictionary<String, FullRedis> _servers2 = new Dictionary<String, FullRedis>();

        private FullRedis GetOrAdd(RedisNode node, Int32 db)
        {
            var key = $"{node.Id}-{db}";
            if (!_servers2.TryGetValue(key, out var rds)) _servers2[key] = rds = new FullRedis();

            rds.Server = node.Server;
            rds.Password = node.Password;
            rds.Db = db;
            rds.Tracer = _tracer;

            return rds;
        }

        public void TraceNode(RedisNode node)
        {
            using var span = _tracer?.NewSpan($"RedisService-TraceNode", node);

            if (!_servers.TryGetValue(node.Id, out var rds)) _servers[node.Id] = rds = new FullRedis();

            // 可能后面更新了服务器地址和密码
            rds.Server = node.Server;
            rds.Password = node.Password;
            rds.Tracer = _tracer;

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
            if (node.ScanQueue && dbs != null && dbs.Length > 0) ScanQueue(node, dbs);
        }

        private void ScanQueue(RedisNode node, RedisDbEntry[] dbs)
        {
            var queues = RedisMessageQueue.FindAllByRedisId(node.Id);

            for (var i = 0; i < dbs.Length; i++)
            {
                if (dbs[i] == null) continue;

                var rds = GetOrAdd(node, i);

                // keys个数太大不支持扫描
                if (rds.Count < 10000)
                {
                    foreach (var item in rds.Search("*:Status:*", 1000))
                    {
                        var ss = item.Split(":");
                        var topic = ss.Take(ss.Length - 2).Join(":");
                        if (topic.IsNullOrEmpty()) continue;

                        // 可信队列
                        {
                            SaveQueue(node, i, queues, topic, "Queue");
                        }

                        // 延迟队列
                        {
                            topic += ":Delay";
                            if (rds.ContainsKey(topic)) SaveQueue(node, i, queues, topic, "Delay");
                        }
                    }
                }
                // 搜索RedisStream队列
                if (rds.Count < 100)
                {
                    foreach (var item in rds.Keys)
                    {
                        var type = rds.Execute(item, r => r.Execute<String>("TYPE", item), false);
                        if (type.EqualIgnoreCase("stream"))
                        {
                            SaveQueue(node, i, queues, item, type);
                        }
                    }
                }
            }
        }

        private void SaveQueue(RedisNode node, Int32 db, IList<RedisMessageQueue> queues, String topic, String type)
        {
            var mq = queues.FirstOrDefault(e => e.Db == db && e.Topic == topic);
            if (mq == null)
            {
                mq = new RedisMessageQueue
                {
                    RedisId = node.Id,
                    Db = db,
                    Topic = topic,
                    Enable = true,
                };

                queues.Add(mq);
            }

            mq.Enable = true;
            if (mq.Name.IsNullOrEmpty()) mq.Name = topic;
            if (mq.Category.IsNullOrEmpty()) mq.Category = node.Category;
            if (mq.Type.IsNullOrEmpty()) mq.Type = type;

            mq.SaveAsync();
        }

        private void DoTraceQueue(Object state)
        {
            var list = RedisMessageQueue.FindAll();
            foreach (var item in list)
            {
                if (item.Enable && item.Redis != null)
                {
                    // 捕获异常，不要影响后续操作
                    var key = $"DoTraceQueue:{item.Id}";
                    var errors = _cache.Get<Int64>(key);
                    if (errors < 5)
                    {
                        try
                        {
                            TraceQueue(item);

                            _cache.Remove(key);
                        }
                        catch (Exception ex)
                        {
                            errors = _cache.Increment(key, 1);
                            if (errors <= 1)
                                _cache.SetExpire(key, TimeSpan.FromMinutes(10));

                            XTrace.WriteException(ex);
                        }
                    }
                    else
                    {
                        item.Enable = false;

                        _cache.Remove(key);
                    }

                    item.SaveAsync();
                }
            }
        }

        public void TraceQueue(RedisMessageQueue queue)
        {
            if (queue.Topic.IsNullOrEmpty()) return;

            using var span = _tracer?.NewSpan($"RedisService-TraceQueue", queue);

            var rds = GetOrAdd(queue.Redis, queue.Db);

            switch (queue.Type?.ToLower())
            {
                case "queue":
                    {
                        var mq = rds.GetQueue<Object>(queue.Topic);
                        queue.Messages = mq.Count;

                        var cs = rds.Search($"{queue.Topic}:Status:*", 1000).ToArray();
                        queue.Consumers = cs.Length;

                        if (cs.Length > 0)
                        {
                            var sts = rds.GetAll<RedisQueueStatus>(cs);
                            if (sts != null)
                            {
                                queue.Total = sts.Sum(e => e.Value.Consumes);
                                queue.FirstConsumer = sts.Min(e => e.Value.CreateTime);
                                queue.LastActive = sts.Max(e => e.Value.LastActive);
                                queue.Remark = sts.ToJson();
                            }
                        }
                        else
                        {
                            queue.Enable = false;
                        }
                    }
                    break;
                case "delay":
                    {
                        var mq = rds.GetDelayQueue<Object>(queue.Topic);
                        queue.Messages = mq.Count;

                        var topic = queue.Topic.TrimEnd(":Delay");
                        //var st = rds.Get<RedisQueueStatus>(topic);

                        var cs = rds.Search($"{topic}:Status:*", 1000).ToArray();
                        queue.Consumers = cs.Length;

                        if (cs.Length > 0)
                        {
                            var sts = rds.GetAll<RedisQueueStatus>(cs);
                            if (sts != null)
                            {
                                queue.Total = sts.Sum(e => e.Value.Consumes);
                                queue.FirstConsumer = sts.Min(e => e.Value.CreateTime);
                                queue.LastActive = sts.Max(e => e.Value.LastActive);
                                queue.Remark = sts.ToJson();
                            }
                        }
                        else
                        {
                            queue.Enable = false;
                        }
                    }
                    break;
                case "stream":
                    {
                        var mq = rds.GetStream<Object>(queue.Topic);
                        //queue.Messages = mq.Count;
                        queue.Total = mq.Count;

                        var gs = mq.GetGroups();
                        if (gs != null)
                        {
                            queue.Consumers = gs.Sum(e => e.Consumers);
                            //queue.Remark = gs.ToJson();
                        }

                        var inf = mq.GetInfo();
                        if (inf != null)
                        {
                            if (!inf.FirstId.IsNullOrEmpty())
                            {
                                var p = inf.FirstId.IndexOf('-');
                                var str = p > 0 ? inf.FirstId[..p] : inf.FirstId;
                                queue.FirstConsumer = str.ToLong().ToDateTime().ToLocalTime();
                            }
                            if (!inf.LastId.IsNullOrEmpty())
                            {
                                var p = inf.LastId.IndexOf('-');
                                var str = p > 0 ? inf.LastId[..p] : inf.LastId;
                                queue.LastActive = str.ToLong().ToDateTime().ToLocalTime();
                            }

                            queue.Remark = inf.ToJson();
                        }
                    }
                    break;
                default:
                    break;
            }
        }
    }
}