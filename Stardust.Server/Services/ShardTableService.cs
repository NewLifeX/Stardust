using NewLife;
using NewLife.Log;
using NewLife.Threading;
using Stardust.Data.Monitors;
using XCode.DataAccessLayer;

namespace Stardust.Server.Services;

/// <summary>分表管理</summary>
public class ShardTableService : IHostedService
{
    private readonly StarServerSetting _setting;
    private readonly ITracer _tracer;
    private TimerX _timer;
    public ShardTableService(StarServerSetting setting, ITracer tracer)
    {
        _setting = setting;
        _tracer = tracer;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        // 每小时执行
        _timer = new TimerX(DoShardTable, null, 5_000, 3600 * 1000) { Async = true };

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _timer.TryDispose();

        return Task.CompletedTask;
    }

    private void DoShardTable(Object state)
    {
        //var set = Setting.Current;
        //if (set.DataRetention <= 0) return;

        // 保留数据的起点
        var today = DateTime.Today;
        var endday = today.AddDays(-_setting.DataRetention);

        XTrace.WriteLine("检查数据分表，保留数据起始日期：{0:yyyy-MM-dd}", endday);

        using var span = _tracer?.NewSpan("ShardTable", $"{endday.ToFullString()}");
        try
        {
            // 取所有表，清空缓存
            var dal = TraceData.Meta.Session.Dal;
            var dal2 = AppTracer.Meta.Session.Dal;

            dal.Tables = null;
            var tables = dal.Tables;
            var tnames = tables.Select(e => e.TableName).ToArray();

            for (var dt = today.AddYears(-10); dt < endday; dt = dt.AddDays(1))
            {
                var name = $"SampleData_{dt:yyyyMMdd}";
                if (name.EqualIgnoreCase(tnames))
                {
                    try
                    {
                        dal.Execute($"Drop Table {name}");
                    }
                    catch (Exception ex)
                    {
                        XTrace.WriteException(ex);
                    }
                }

                name = $"TraceData_{dt:yyyyMMdd}";
                if (name.EqualIgnoreCase(tnames))
                {
                    try
                    {
                        dal.Execute($"Drop Table {name}");
                    }
                    catch (Exception ex)
                    {
                        XTrace.WriteException(ex);
                    }
                }
            }

            // 数据迁移后，原库数据表需要清理。需要重新获取表名列表，因为Stardust/StardustData可能指向同一个数据库
            dal2.Tables = null;
            var tnames2 = dal2.Tables.Select(e => e.TableName).ToArray();
            for (var dt = today.AddYears(-10); dt < endday; dt = dt.AddDays(1))
            {
                var name = $"SampleData_{dt:yyyyMMdd}";
                if (name.EqualIgnoreCase(tnames2))
                {
                    try
                    {
                        dal2.Execute($"Drop Table {name}");
                    }
                    catch { }
                }

                name = $"TraceData_{dt:yyyyMMdd}";
                if (name.EqualIgnoreCase(tnames2))
                {
                    try
                    {
                        dal2.Execute($"Drop Table {name}");
                    }
                    catch { }
                }
            }

            // 新建今天明天的表
            var ts = new List<IDataTable>();
            {
                var table = TraceData.Meta.Table.DataTable.Clone() as IDataTable;
                table.TableName = $"TraceData_{today:yyyyMMdd}";
                ts.Add(table);
            }
            {
                var table = TraceData.Meta.Table.DataTable.Clone() as IDataTable;
                table.TableName = $"TraceData_{today.AddDays(1):yyyyMMdd}";
                ts.Add(table);
            }
            {
                var table = SampleData.Meta.Table.DataTable.Clone() as IDataTable;
                table.TableName = $"SampleData_{today:yyyyMMdd}";
                ts.Add(table);
            }
            {
                var table = SampleData.Meta.Table.DataTable.Clone() as IDataTable;
                table.TableName = $"SampleData_{today.AddDays(1):yyyyMMdd}";
                ts.Add(table);
            }

            if (ts.Count > 0)
            {
                XTrace.WriteLine("创建或更新数据表[{0}]：{1}", ts.Count, ts.Join(",", e => e.TableName));

                //dal.SetTables(ts.ToArray());
                dal.Db.CreateMetaData().SetTables(Migration.On, ts.ToArray());

                // 首次建表时，设置为压缩表
                if (dal.DbType == DatabaseType.MySql)
                {
                    foreach (var dt in ts)
                    {
                        if (!dt.TableName.EqualIgnoreCase(tnames))
                            dal.Execute($"Alter Table {dt.TableName} ROW_FORMAT=COMPRESSED KEY_BLOCK_SIZE=4");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            span?.SetError(ex, null);
            throw;
        }

        XTrace.WriteLine("检查数据表完成");
    }
}