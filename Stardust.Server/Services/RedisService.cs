using NewLife;
using NewLife.Caching;
using NewLife.Caching.Queues;
using NewLife.Log;
using NewLife.Serialization;
using NewLife.Threading;
using Stardust.Data.Models;
using Stardust.Data.Nodes;
using XCode.Membership;

namespace Stardust.Server.Services;

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
    private readonly NewLife.Log.ILog _log;

    public RedisService(ITracer tracer, NewLife.Log.ILog log)
    {
        _tracer = tracer;
        _log = log;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        // 初始化定时器
        _traceNode = new TimerX(DoTraceNode, null, 60_000, Period * 1000) { Async = true };
        _traceQueue = new TimerX(DoTraceQueue, null, 65_000, Period * 1000) { Async = true };

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
        if (!node.UserName.IsNullOrEmpty())
        {
            rds.UserName = node.UserName;
        }
        rds.Db = db;
        rds.Tracer = _tracer;
        rds.Log = _log;

        return rds;
    }

    public void TraceNode(RedisNode node)
    {
        using var span = _tracer?.NewSpan($"RedisService-TraceNode", node);

        if (!_servers.TryGetValue(node.Id, out var rds)) _servers[node.Id] = rds = new FullRedis();

        // 可能后面更新了服务器地址和密码
        rds.Server = node.Server;
        if (!node.UserName.IsNullOrEmpty())
        {
            rds.UserName = node.UserName;
        }
        rds.Password = node.Password;
        rds.Tracer = _tracer;
        rds.Log = _log;

        //var inf = rds.GetInfo(true);
        var inf = rds.GetInfo(false);
        node.Fill(inf);
        node.Update();

        var data = new RedisData
        {
            RedisId = node.Id,
            Name = node.Name,
        };
        var dbs = data.Fill(inf);
        data.Insert();

        // 扫描队列
        if (node.ScanQueue && dbs != null && dbs.Length > 0) ScanQueue(node, dbs);

        // 自动发现集群和哨兵节点
        DiscoverNodes(node, rds);
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
                foreach (var item in rds.Search("*:Status:*", 0, 1000))
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
                    var type = rds.Execute(item, (r, k) => r.Execute<String>("TYPE", k), false);
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

        //mq.Enable = true;
        if (mq.Name.IsNullOrEmpty()) mq.Name = topic;
        if (mq.Category.IsNullOrEmpty()) mq.Category = node.Category;
        if (mq.Type.IsNullOrEmpty()) mq.Type = type;

        mq.Save();
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

                item.Update();
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

                    var cs = rds.Search($"{queue.Topic}:Status:*", 0, 1000).ToArray();
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

                    var cs = rds.Search($"{topic}:Status:*", 0, 1000).ToArray();
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
                        queue.Groups = gs.Join(",", e => e.Name);
                        queue.Consumers = gs.Sum(e => e.Consumers);
                        queue.Messages = gs.Sum(e => e.Pending);
                        //queue.Remark = gs.ToJson();

                        if (gs.Length > 0)
                        {
                            var dic = new Dictionary<String, Object>();
                            foreach (var g in gs)
                            {
                                var cs = mq.GetConsumers(g.Name);
                                if (cs != null && cs.Length > 0) dic.Add(g.Name, cs);
                            }
                            queue.ConsumerInfo = dic.ToJson();
                        }
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

    /// <summary>自动发现Redis集群和哨兵节点</summary>
    /// <param name="node">当前Redis节点</param>
    /// <param name="rds">Redis客户端</param>
    private void DiscoverNodes(RedisNode node, FullRedis rds)
    {
        if (node.Mode.IsNullOrEmpty()) return;

        try
        {
            if (node.Mode.EqualIgnoreCase("cluster"))
            {
                DiscoverClusterNodes(node, rds);
            }
            else if (node.Mode.EqualIgnoreCase("sentinel"))
            {
                DiscoverSentinelNodes(node, rds);
            }
        }
        catch (Exception ex)
        {
            XTrace.WriteException(ex);
        }
    }

    /// <summary>发现集群节点</summary>
    /// <param name="node">当前Redis节点</param>
    /// <param name="rds">Redis客户端</param>
    private void DiscoverClusterNodes(RedisNode node, FullRedis rds)
    {
        using var span = _tracer?.NewSpan("RedisService-DiscoverClusterNodes", node);

        // 执行 CLUSTER NODES 命令获取集群节点信息
        var result = rds.Execute(null, (r, k) => r.Execute<String>("CLUSTER", "NODES"));
        if (result.IsNullOrEmpty()) return;

        var lines = result.Split('\n');
        foreach (var line in lines)
        {
            if (line.IsNullOrWhiteSpace()) continue;

            // 每行格式: <id> <ip:port@cport> <flags> <master> <ping-sent> <pong-recv> <config-epoch> <link-state> <slot> <slot> ... <slot>
            var parts = line.Split(' ');
            if (parts.Length < 2) continue;

            // 解析地址信息 ip:port@cport 或 ip:port
            var address = parts[1];
            var atPos = address.IndexOf('@');
            if (atPos > 0) address = address[..atPos];

            // 跳过当前节点
            if (address.EqualIgnoreCase(node.Server)) continue;

            // 检查节点是否已存在
            var existingNode = RedisNode.FindByServer(address);
            if (existingNode != null) continue;

            // 创建新节点
            var newNode = new RedisNode
            {
                Name = $"{node.Name}-{address}",
                Category = node.Category,
                Server = address,
                UserName = node.UserName,
                Password = node.Password,
                ProjectId = node.ProjectId,
                Enable = true,
                ScanQueue = node.ScanQueue,
                WebHook = node.WebHook,
                AlarmMemoryRate = node.AlarmMemoryRate,
                AlarmConnections = node.AlarmConnections,
                AlarmSpeed = node.AlarmSpeed,
                AlarmInputKbps = node.AlarmInputKbps,
                AlarmOutputKbps = node.AlarmOutputKbps,
            };
            newNode.Insert();

            XTrace.WriteLine("自动添加集群节点: {0}", address);
            WriteLog("DiscoverClusterNodes", true, $"自动添加集群节点 [{address}] 从 [{node.Server}]");
        }
    }

    /// <summary>发现哨兵节点</summary>
    /// <param name="node">当前Redis节点</param>
    /// <param name="rds">Redis客户端</param>
    private void DiscoverSentinelNodes(RedisNode node, FullRedis rds)
    {
        using var span = _tracer?.NewSpan("RedisService-DiscoverSentinelNodes", node);

        try
        {
            // 获取所有master
            var masters = rds.Execute(null, (r, k) => r.Execute<Object[]>("SENTINEL", "MASTERS"));
            if (masters != null && masters.Length > 0)
            {
                DiscoverSentinelMasters(node, rds, masters);
            }
        }
        catch (Exception ex)
        {
            XTrace.WriteException(ex);
        }

        try
        {
            // 获取哨兵节点信息
            var sentinels = rds.Execute(null, (r, k) => r.Execute<Object[]>("SENTINEL", "SENTINELS", "mymaster"));
            if (sentinels != null && sentinels.Length > 0)
            {
                ProcessSentinelList(node, sentinels, "sentinel");
            }
        }
        catch (Exception ex)
        {
            // 可能没有配置master名称为mymaster，忽略此错误
            XTrace.Log.Debug("获取哨兵节点失败: {0}", ex.Message);
        }
    }

    /// <summary>发现哨兵主从节点</summary>
    /// <param name="node">当前Redis节点</param>
    /// <param name="rds">Redis客户端</param>
    /// <param name="masters">主节点列表</param>
    private void DiscoverSentinelMasters(RedisNode node, FullRedis rds, Object[] masters)
    {
        foreach (var masterObj in masters)
        {
            if (masterObj is not Object[] master || master.Length == 0) continue;

            // 解析master信息，格式为key-value对
            var masterInfo = ParseRedisArray(master);
            if (!masterInfo.TryGetValue("name", out var masterName)) continue;

            // 添加master节点
            if (masterInfo.TryGetValue("ip", out var ip) && masterInfo.TryGetValue("port", out var port))
            {
                var address = $"{ip}:{port}";
                AddRedisNode(node, address, "master");
            }

            // 获取该master的所有slaves
            try
            {
                var slaves = rds.Execute(null, (r, k) => r.Execute<Object[]>("SENTINEL", "SLAVES", masterName));
                if (slaves != null && slaves.Length > 0)
                {
                    ProcessSentinelList(node, slaves, "slave");
                }
            }
            catch (Exception ex)
            {
                XTrace.Log.Debug("获取从节点失败 [{0}]: {1}", masterName, ex.Message);
            }
        }
    }

    /// <summary>处理哨兵节点列表</summary>
    /// <param name="node">当前Redis节点</param>
    /// <param name="list">节点列表</param>
    /// <param name="role">角色</param>
    private void ProcessSentinelList(RedisNode node, Object[] list, String role)
    {
        foreach (var itemObj in list)
        {
            if (itemObj is not Object[] item || item.Length == 0) continue;

            var info = ParseRedisArray(item);
            if (info.TryGetValue("ip", out var ip) && info.TryGetValue("port", out var port))
            {
                var address = $"{ip}:{port}";
                AddRedisNode(node, address, role);
            }
        }
    }

    /// <summary>解析Redis返回的数组为字典</summary>
    /// <param name="array">Redis数组</param>
    /// <returns>字典</returns>
    private static IDictionary<String, String> ParseRedisArray(Object[] array)
    {
        var dict = new Dictionary<String, String>();
        
        // Redis 返回的数组应该是偶数长度（key-value对）
        if (array.Length % 2 != 0)
        {
            XTrace.WriteLine("警告: Redis 返回的数组长度为奇数 {0}，最后一个元素将被忽略", array.Length);
        }

        for (var i = 0; i < array.Length - 1; i += 2)
        {
            var key = array[i]?.ToString();
            var value = array[i + 1]?.ToString();
            if (!key.IsNullOrEmpty() && !value.IsNullOrEmpty())
            {
                dict[key] = value;
            }
        }
        return dict;
    }

    /// <summary>添加Redis节点</summary>
    /// <param name="parentNode">父节点</param>
    /// <param name="address">节点地址</param>
    /// <param name="role">节点角色</param>
    private void AddRedisNode(RedisNode parentNode, String address, String role)
    {
        // 跳过当前节点
        if (address.EqualIgnoreCase(parentNode.Server)) return;

        // 检查节点是否已存在
        var existingNode = RedisNode.FindByServer(address);
        if (existingNode != null) return;

        // 创建新节点
        var newNode = new RedisNode
        {
            Name = $"{parentNode.Name}-{role}-{address}",
            Category = parentNode.Category,
            Server = address,
            UserName = parentNode.UserName,
            Password = parentNode.Password,
            ProjectId = parentNode.ProjectId,
            Enable = true,
            ScanQueue = parentNode.ScanQueue,
            WebHook = parentNode.WebHook,
            AlarmMemoryRate = parentNode.AlarmMemoryRate,
            AlarmConnections = parentNode.AlarmConnections,
            AlarmSpeed = parentNode.AlarmSpeed,
            AlarmInputKbps = parentNode.AlarmInputKbps,
            AlarmOutputKbps = parentNode.AlarmOutputKbps,
        };
        newNode.Insert();

        XTrace.WriteLine("自动添加{0}节点: {1}", role, address);
        WriteLog("DiscoverSentinelNodes", true, $"自动添加{role}节点 [{address}] 从 [{parentNode.Server}]");
    }

    /// <summary>写日志</summary>
    /// <param name="action">操作</param>
    /// <param name="success">是否成功</param>
    /// <param name="remark">备注</param>
    private static void WriteLog(String action, Boolean success, String remark)
    {
        LogProvider.Provider?.WriteLog("RedisNode", action, success, remark);
    }
}