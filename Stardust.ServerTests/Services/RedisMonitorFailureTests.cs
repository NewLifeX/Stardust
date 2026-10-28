using System;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NewLife;
using NewLife.Log;
using NewLife.Security;
using Stardust.Data.Models;
using Stardust.Data.Nodes;
using Stardust.Server.Services;
using Xunit;

namespace ServerTest.Services;

/// <summary>Redis 监控服务容错测试。覆盖采集失败计数、连续失败后自动停用节点与启停生命周期（DBM-6）</summary>
public class RedisMonitorFailureTests
{
    /// <summary>调用私有的采集循环，模拟一次定时器触发</summary>
    private static void RunTrace(RedisService service) =>
        typeof(RedisService).GetMethod("DoTraceNode", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(service, [null]);

    /// <summary>绕过实体缓存直接查库读取启用状态</summary>
    private static Boolean? ReadEnable(Int32 id)
    {
        var dt = RedisNode.Meta.Session.Query($"select Enable from {RedisNode.Meta.TableName} where Id={id}");
        if (dt == null || dt.Rows.Count == 0) return null;

        return dt.Rows[0][0].ToBoolean();
    }

    [Fact(DisplayName = "采集连续失败五次后自动停用节点")]
    public async Task TraceNode_FailFiveTimes_DisablesNode()
    {
        var service = new RedisService(null, Logger.Null);

        var node = new RedisNode
        {
            Name = "redis-" + Rand.NextString(6),
            Server = "127.0.0.1:2",
            Enable = true,
            ScanQueue = false,
        };
        node.Insert();

        try
        {
            // 失败计数在异常分支内递增，第 5 次失败后计数达到阈值，第 6 次进入停用分支
            for (var i = 0; i < 6; i++) RunTrace(service);

            // 停用是异步落库，轮询等待
            var sw = Stopwatch.StartNew();
            Boolean? enable = null;
            while (sw.ElapsedMilliseconds < 10000)
            {
                enable = ReadEnable(node.Id);
                if (enable == false) break;

                await Task.Delay(100);
            }

            Assert.False(enable);
        }
        finally
        {
            RedisNode.FindById(node.Id)?.Delete();
        }
    }

    [Fact(DisplayName = "采集异常会计入失败次数")]
    public void TraceNode_Error_IncrementsCounter()
    {
        var service = new RedisService(null, Logger.Null);

        var node = new RedisNode
        {
            Name = "redis-" + Rand.NextString(6),
            Server = "127.0.0.1:3",
            Enable = true,
            ScanQueue = false,
        };
        node.Insert();

        try
        {
            RunTrace(service);

            var cache = (NewLife.Caching.MemoryCache)typeof(RedisService)
                .GetField("_cache", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(service);
            var errors = cache.Get<Int64>($"DoTraceNode:{node.Id}");

            Assert.True(errors >= 1, $"失败次数应大于等于1，实际 {errors}");
        }
        finally
        {
            RedisNode.FindById(node.Id)?.Delete();
        }
    }

    [Fact(DisplayName = "未启用节点不参与采集，状态保持不变")]
    public void TraceNode_DisabledNode_Skip()
    {
        var service = new RedisService(null, Logger.Null);

        var node = new RedisNode
        {
            Name = "redis-" + Rand.NextString(6),
            Server = "127.0.0.1:1",
            Enable = false,
        };
        node.Insert();

        try
        {
            RunTrace(service);

            Assert.False(ReadEnable(node.Id));
        }
        finally
        {
            RedisNode.FindById(node.Id)?.Delete();
        }
    }

    [Fact(DisplayName = "采集启动与停止不抛异常")]
    public async Task StartStop_NoThrow()
    {
        var service = new RedisService(null, Logger.Null) { Period = 60 };

        await service.StartAsync(CancellationToken.None);
        await service.StopAsync(CancellationToken.None);
    }
}
