using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using NewLife;
using NewLife.Log;
using NewLife.Threading;
using Stardust.Data.Monitors;
using XCode.DataAccessLayer;

namespace Stardust.Server.Services
{
    /// <summary>分表管理</summary>
    public class ShardTableService : IHostedService
    {
        private readonly ITracer _tracer;
        private TimerX _timer;
        public ShardTableService(ITracer tracer) => _tracer = tracer;

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
            var set = Setting.Current;
            //if (set.DataRetention <= 0) return;

            // 保留数据的起点
            var today = DateTime.Today;
            var endday = today.AddDays(-set.DataRetention);

            XTrace.WriteLine("检查数据分表，保留数据起始日期：{0:yyyy-MM-dd}", endday);

            using var span = _tracer?.NewSpan("ShardTable", $"{endday.ToFullString()}");
            try
            {
                // 所有表
                var dal = TraceData.Meta.Session.Dal;
                var tables = dal.Tables;
                var tnames = tables.Select(e => e.TableName).ToArray();

                for (var dt = today.AddYears(-1); dt < endday; dt = dt.AddDays(1))
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
}