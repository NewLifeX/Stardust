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
    private TimerX _timer2;
    public ShardTableService(StarServerSetting setting, ITracer tracer)
    {
        _setting = setting;
        _tracer = tracer;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        // 每小时执行
        _timer = new TimerX(DoShardTable, null, 15_000, 3600 * 1000) { Async = true };
        _timer2 = new TimerX(DoClearDetails, null, 60_000, 600 * 1000) { Async = true };

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _timer2.TryDispose();
        _timer.TryDispose();

        return Task.CompletedTask;
    }

    private void DoShardTable(Object state)
    {
        // 保留数据的起点
        var days = _setting.DataRetention;
        var now = DateTime.Now;
        var startTime = now.AddDays(-days);

        using var span = _tracer?.NewSpan("ShardTable", $"{startTime.ToFullString()}");
        try
        {
            // 取所有表，清空缓存
            var dal = TraceData.Meta.Session.Dal;

            dal.Tables = null;
            var tables = dal.Tables;
            var tnames = tables.Select(e => e.TableName).ToArray();

            // 检查表结构，共31张表，循环使用
            var ts = new List<IDataTable>();
            for (var i = 0; i < 31; i++)
            {
                var date = i + 1;

                {
                    var table = TraceData.Meta.Table.DataTable.Clone() as IDataTable;
                    table.TableName = $"TraceData_{date:00}";
                    ts.Add(table);
                }
                {
                    var table = SampleData.Meta.Table.DataTable.Clone() as IDataTable;
                    table.TableName = $"SampleData_{date:00}";
                    ts.Add(table);
                }
            }

            if (ts.Count > 0)
            {
                XTrace.WriteLine("检查循环天表[{0}]：{1}", ts.Count, ts.Join(",", e => e.TableName));

                //dal.SetTables(ts.ToArray());
                dal.Db.CreateMetaData().SetTables(Migration.On, ts.ToArray());

                // 首次建表时，设置为压缩表
                if (dal.DbType == DatabaseType.MySql)
                {
                    foreach (var dt in ts)
                    {
                        if (!tnames.Any(e => e.EqualIgnoreCase(dt.TableName, dt.Name)))
                            dal.Execute($"Alter Table {dt.TableName} ROW_FORMAT=COMPRESSED KEY_BLOCK_SIZE=4");
                    }
                }
            }

            // 数据迁移后，原库数据表需要清理。需要重新获取表名列表，因为Stardust/StardustData可能指向同一个数据库
            DropOldTable(dal);
            var dal2 = AppTracer.Meta.Session.Dal;
            DropOldTable(dal2);
        }
        catch (Exception ex)
        {
            span?.SetError(ex, null);
            throw;
        }
    }

    private void DoClearDetails(Object state)
    {
        // 保留数据的起点
        var days = _setting.DataRetention;
        var now = DateTime.Now;
        var startTime = now.AddDays(-days);

        XTrace.WriteLine("检查数据分表，保留数据{0}天，起始时间：{1}", days, startTime);

        var rs = 0;
        using var span = _tracer?.NewSpan("ClearDetails", $"{startTime.ToFullString()}");
        try
        {
            // 取所有表，清空缓存
            var dal = TraceData.Meta.Session.Dal;

            dal.Tables = null;
            var tnames = dal.Tables.Select(e => e.TableName).ToArray();

            // 如果保留时间超过了31天，则使用删除功能清理历史数据，否则使用truncate
            if (days > 31 || _setting.ClearMode == ClearModes.Delete)
            {
                // 31张表里面，每张表都删除指定时间之前的数据
                for (var i = 0; i < 31; i++)
                {
                    var dt = now.AddDays(-i);
                    rs += TraceData.DeleteBefore(dt, startTime);
                    rs += SampleData.DeleteBefore(dt, startTime);
                }
            }
            else
            {
                using var showSql = dal.Session.SetShowSql(true);

                // 遍历31张表，只要大于结束时间则安全，否则清空
                for (var i = 0; i < 31; i++)
                {
                    var dt = now.AddDays(-i);
                    if (dt >= startTime) continue;

                    //!! 对于不足31天的月份，要注意不要越过今天的天表
                    if (dt.Day == now.Day) break;

                    var name = $"TraceData_{dt:dd}";
                    if (name.EqualIgnoreCase(tnames))
                    {
                        try
                        {
                            if (dal.DbType == DatabaseType.SQLite || _setting.ClearMode == ClearModes.Delete)
                                rs += TraceData.DeleteBefore(dt, startTime);
                            else
                                rs += dal.Execute($"Truncate Table {name}");
                        }
                        catch (Exception ex)
                        {
                            XTrace.WriteException(ex);
                        }
                    }
                    name = $"SampleData_{dt:dd}";
                    if (name.EqualIgnoreCase(tnames))
                    {
                        try
                        {
                            if (dal.DbType == DatabaseType.SQLite || _setting.ClearMode == ClearModes.Delete)
                                rs += SampleData.DeleteBefore(dt, startTime);
                            else
                                rs += dal.Execute($"Truncate Table {name}");
                        }
                        catch (Exception ex)
                        {
                            XTrace.WriteException(ex);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            span?.SetError(ex, null);
            throw;
        }

        if (span != null) span.Value = rs;

        XTrace.WriteLine("检查数据表完成");
    }

    static void DropOldTable(DAL dal)
    {
        using var showSql = dal.Session.SetShowSql(true);

        dal.Tables = null;
        var tnames2 = dal.Tables.Select(e => e.TableName).ToArray();
        var today = DateTime.Today;
        for (var dt = today.AddYears(-10); dt <= today; dt = dt.AddDays(1))
        {
            var name = $"SampleData_{dt:yyyyMMdd}";
            if (name.EqualIgnoreCase(tnames2))
            {
                try
                {
                    dal.Execute($"Drop Table {name}");
                }
                catch { }
            }

            name = $"TraceData_{dt:yyyyMMdd}";
            if (name.EqualIgnoreCase(tnames2))
            {
                try
                {
                    dal.Execute($"Drop Table {name}");
                }
                catch { }
            }
        }
    }

    /// <summary>修正自动分表。20250103启用新的循环天表</summary>
    public static void FixShardTable()
    {
        var dal = TraceData.Meta.Session.Dal;
        var tables = dal.Tables;
        if (tables == null) return;

        using var showSql = dal.Session.SetShowSql(true);

        // 从明天起倒推31天，保证31张表，如果旧表则重命名
        var today = DateTime.Today;

        // 新建缺失表
        //var ts = new List<IDataTable>();
        for (var i = 0; i < 31; i++)
        {
            var date = today.AddDays(1 - i);

            var newName = $"TraceData_{date:dd}";
            var table = tables.FirstOrDefault(e => e.TableName.EqualIgnoreCase(newName));
            if (table == null)
            {
                var oldName = $"TraceData_{date:yyyyMMdd}";
                table = tables.FirstOrDefault(e => e.TableName.EqualIgnoreCase(oldName));
                if (table != null)
                    dal.Execute($"Alter Table {table.TableName} Rename To {newName}");
                //else
                //{
                //    table = TraceData.Meta.Table.DataTable.Clone() as IDataTable;
                //    table.TableName = newName;
                //    ts.Add(table);
                //}
            }

            newName = $"SampleData_{date:dd}";
            table = tables.FirstOrDefault(e => e.TableName.EqualIgnoreCase(newName));
            if (table == null)
            {
                var oldName = $"SampleData_{date:yyyyMMdd}";
                table = tables.FirstOrDefault(e => e.TableName.EqualIgnoreCase(oldName));
                if (table != null)
                    dal.Execute($"Alter Table {table.TableName} Rename To {newName}");
                //else
                //{
                //    table = SampleData.Meta.Table.DataTable.Clone() as IDataTable;
                //    table.TableName = newName;
                //    ts.Add(table);
                //}
            }
        }

        //if (ts.Count > 0)
        //{
        //    XTrace.WriteLine("迁移到循环天表[{0}]：{1}", ts.Count, ts.Join(",", e => e.TableName));

        //    //dal.SetTables(ts.ToArray());
        //    dal.Db.CreateMetaData().SetTables(Migration.On, ts.ToArray());

        //    // 首次建表时，设置为压缩表
        //    if (dal.DbType == DatabaseType.MySql)
        //    {
        //        foreach (var dt in ts)
        //        {
        //            dal.Execute($"Alter Table {dt.TableName} ROW_FORMAT=COMPRESSED KEY_BLOCK_SIZE=4");
        //        }
        //    }
        //}
    }
}