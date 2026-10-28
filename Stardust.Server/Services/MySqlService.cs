using System.Data;
using NewLife;
using NewLife.Caching;
using NewLife.Log;
using NewLife.Threading;
using Stardust.Data.Nodes;
using XCode.DataAccessLayer;

namespace Stardust.Server.Services;

/// <summary>MySQL 监控服务接口。供管理页面手动触发采集</summary>
public interface IMySqlService
{
    /// <summary>采集单个 MySQL 节点的版本与运行状态</summary>
    /// <param name="node">MySQL 节点</param>
    void TraceNode(MySqlNode node);
}

/// <summary>MySQL 监控服务。定时采集节点版本与全局状态，连续失败 5 次后停用节点</summary>
public class MySqlService : IHostedService, IMySqlService
{
    /// <summary>计算周期。默认60秒</summary>
    public Int32 Period { get; set; } = 60;

    private TimerX _traceNode;
    private readonly ICache _cache = new MemoryCache();
    private readonly ITracer _tracer;
    private readonly ILog _log;

    public MySqlService(ITracer tracer, ILog log)
    {
        _tracer = tracer;
        _log = log;
    }

    /// <summary>启动定时器。首次采集在 60 秒后开始，避免与应用启动争抢资源</summary>
    /// <param name="cancellationToken">取消通知</param>
    /// <returns>已完成的任务</returns>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        // 初始化定时器
        _traceNode = new TimerX(DoTraceNode, null, 60_000, Period * 1000) { Async = true };

        return Task.CompletedTask;
    }

    /// <summary>停止并释放定时器</summary>
    /// <param name="cancellationToken">取消通知</param>
    /// <returns>已完成的任务</returns>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _traceNode?.TryDispose();

        return Task.CompletedTask;
    }

    private void DoTraceNode(Object state)
    {
        var list = MySqlNode.FindAllWithCache();
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

    /// <summary>采集单个 MySQL 节点的版本与运行状态，并落库保存采样数据</summary>
    /// <param name="node">MySQL 节点</param>
    public void TraceNode(MySqlNode node)
    {
        using var span = _tracer?.NewSpan($"MySqlService-TraceNode", node);

        // 构建连接字符串
        var connStr = $"Server={node.Server};Port={node.Port};Database={node.DatabaseName ?? "mysql"};Uid={node.UserName};Pwd={node.Password};";

        var connName = "MySqlMonitor_" + node.Id;
        DAL.AddConnStr(connName, connStr, null, "MySql");
        var dal = DAL.Create(connName);

        // 获取MySQL版本信息
        var versionResult = dal.Session.Query("SELECT VERSION() as Ver");
        if (versionResult != null && versionResult.Tables.Count > 0 && versionResult.Tables[0].Rows.Count > 0)
        {
            var version = versionResult.Tables[0].Rows[0]["Ver"] + "";
            if (!version.IsNullOrEmpty())
                node.Version = version.Substring(0, Math.Min(50, version.Length));
        }
        node.Update();

        // 获取MySQL状态信息
        var sql = "SHOW GLOBAL STATUS";
        var result = dal.Session.Query(sql);
        var status = new Dictionary<String, String>();
        if (result != null && result.Tables.Count > 0)
        {
            var dt = result.Tables[0];
            foreach (DataRow row in dt.Rows)
            {
                var name = row["Variable_name"] + "";
                var value = row["Value"] + "";
                if (!name.IsNullOrEmpty() && !value.IsNullOrEmpty())
                    status[name] = value;
            }
        }

        // 创建监控数据
        var data = new MySqlData
        {
            MySqlId = node.Id,
            Name = node.Name,
        };
        data.Fill(status);
        data.Insert();
    }
}
