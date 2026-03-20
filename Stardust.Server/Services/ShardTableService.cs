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
        using var span = _tracer?.NewSpan("ShardTable");
        try
        {
            var dal = TraceData.Meta.Session.Dal;

            if (dal.DbType == DatabaseType.SQLite)
            {
                // SQLite：各天数据分布在独立数据库文件（Trace01.db~Trace31.db），逐库检查表结构
                for (var dd = 1; dd <= 31; dd++)
                {
                    var table1 = TraceData.Meta.Table.DataTable.Clone() as IDataTable;
                    table1.TableName = $"TraceData_{dd:00}";

                    var table2 = SampleData.Meta.Table.DataTable.Clone() as IDataTable;
                    table2.TableName = $"SampleData_{dd:00}";

                    var dalTrace = DAL.Create($"Trace{dd:00}");
                    dalTrace.Db.CreateMetaData().SetTables(Migration.On, [table1, table2]);
                }
            }
            else
            {
                // 取现有表名，用于判断是否首次建表（MySQL 需要设置压缩格式）
                dal.Tables = null;
                var tnames = dal.Tables.Select(e => e.TableName).ToArray();

                // 构建全部 31 张循环天表的结构定义，一次性提交
                var ts = new List<IDataTable>();
                for (var dd = 1; dd <= 31; dd++)
                {
                    var table1 = TraceData.Meta.Table.DataTable.Clone() as IDataTable;
                    table1.TableName = $"TraceData_{dd:00}";
                    ts.Add(table1);

                    var table2 = SampleData.Meta.Table.DataTable.Clone() as IDataTable;
                    table2.TableName = $"SampleData_{dd:00}";
                    ts.Add(table2);
                }

                XTrace.WriteLine("检查循环天表[{0}]：{1}", ts.Count, ts.Join(",", e => e.TableName));
                dal.Db.CreateMetaData().SetTables(Migration.On, ts.ToArray());

                // 首次建表时，为 MySQL 设置压缩存储格式
                if (dal.DbType == DatabaseType.MySql)
                {
                    foreach (var dt in ts)
                    {
                        if (!tnames.Any(e => e.EqualIgnoreCase(dt.TableName, dt.Name)))
                            dal.Execute($"Alter Table {dt.TableName} ROW_FORMAT=COMPRESSED KEY_BLOCK_SIZE=4");
                    }
                }
            }

            // 清理旧版按完整日期命名的分表（yyyyMMdd 格式）
            // 需重新获取表名列表，因为 Stardust/StardustData 可能指向同一个数据库
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
        var days = _setting.DataRetention;
        if (days <= 0) return; // 0 表示不限制保留期，跳过清理

        var now = DateTime.Now;
        var startTime = now.AddDays(-days);

        XTrace.WriteLine("检查数据分表，保留数据{0}天，起始时间：{1}", days, startTime);

        var rs = 0;
        using var span = _tracer?.NewSpan("ClearDetails", $"{startTime.ToFullString()}");
        try
        {
            var dal = TraceData.Meta.Session.Dal;
            var isSqlite = dal.DbType == DatabaseType.SQLite;

            // 非SQLite：预先获取表名，用于 Truncate 前确认表存在
            String[] tnames = [];
            if (!isSqlite)
            {
                dal.Tables = null;
                tnames = dal.Tables.Select(e => e.TableName).ToArray();
            }

            // 按日序号 01~31 逐表判断，而非按日历往回推（避免遗漏 DD=31 等短月特殊情况）
            for (var dd = 1; dd <= 31; dd++)
            {
                //!! 今天的表正在写入，不能清理
                if (dd == now.Day) continue;

                // 找出该日序号在今天之前最近一次出现的实际日期，用于定位分片
                var mostRecent = GetMostRecentDate(now, dd);
                if (mostRecent == DateTime.MinValue) continue;

                if (mostRecent >= startTime)
                {
                    // 表内含保留窗口内的最新数据，但上一周期的旧数据需要选择性删除
                    // DeleteBefore 内部通过 Meta.CreateShard(mostRecent) 自动路由到正确的分片（含SQLite独立库）
                    rs += TraceData.DeleteBefore(mostRecent, startTime, 1_000_000);
                    rs += SampleData.DeleteBefore(mostRecent, startTime, 1_000_000);
                }
                else
                {
                    // 该日序号所有数据均已超出保留期，整表清空效率更高
                    if (isSqlite)
                    {
                        // SQLite：Drop+Recreate 比逐行 Delete 高效，Vacuum 收缩数据库文件
                        DropAndRecreateSqliteDay(dd);
                    }
                    else if (_setting.ClearMode != ClearModes.Delete)
                    {
                        // MySQL 等：Truncate 整表清空
                        var traceName = $"TraceData_{dd:00}";
                        var sampleName = $"SampleData_{dd:00}";
                        using var showSql = dal.Session.SetShowSql(true);
                        try
                        {
                            if (traceName.EqualIgnoreCase(tnames))
                                rs += dal.Execute($"Truncate Table {traceName}");
                            if (sampleName.EqualIgnoreCase(tnames))
                                rs += dal.Execute($"Truncate Table {sampleName}");
                        }
                        catch (Exception ex)
                        {
                            XTrace.WriteException(ex);
                        }
                    }
                    else
                    {
                        // 强制 Delete 模式：逐行删除
                        rs += TraceData.DeleteBefore(mostRecent, startTime, 1_000_000);
                        rs += SampleData.DeleteBefore(mostRecent, startTime, 1_000_000);
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

    /// <summary>计算指定日序号在今天之前最近一次出现的日期，用于定位循环天表分片</summary>
    /// <param name="now">当前时间</param>
    /// <param name="dd">日序号（1~31）</param>
    static DateTime GetMostRecentDate(DateTime now, Int32 dd)
    {
        // 本月该日已经过了，直接返回本月该日
        if (dd < now.Day)
            return new DateTime(now.Year, now.Month, dd);

        // 本月该日尚未到来，从上个月起往回找（部分月份不含 29/30/31 日）
        var year = now.Year;
        var month = now.Month;
        for (var i = 0; i < 12; i++)
        {
            if (--month == 0) { month = 12; year--; }
            if (dd <= DateTime.DaysInMonth(year, month))
                return new DateTime(year, month, dd);
        }
        return DateTime.MinValue;
    }

    /// <summary>SQLite 整表重建：Drop+Recreate 清空数据，Vacuum 收缩数据库文件</summary>
    /// <param name="dd">日序号（1~31）</param>
    static void DropAndRecreateSqliteDay(Int32 dd)
    {
        var dalTrace = DAL.Create($"Trace{dd:00}");
        using var showSql = dalTrace.Session.SetShowSql(true);
        try
        {
            dalTrace.Execute($"Drop Table If Exists TraceData_{dd:00}");
            dalTrace.Execute($"Drop Table If Exists SampleData_{dd:00}");

            var table1 = TraceData.Meta.Table.DataTable.Clone() as IDataTable;
            table1.TableName = $"TraceData_{dd:00}";
            var table2 = SampleData.Meta.Table.DataTable.Clone() as IDataTable;
            table2.TableName = $"SampleData_{dd:00}";
            dalTrace.Db.CreateMetaData().SetTables(Migration.On, [table1, table2]);

            // 收缩数据库文件，释放清空后的磁盘空间
            dalTrace.Execute("VACUUM");
        }
        catch (Exception ex)
        {
            XTrace.WriteException(ex);
        }
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