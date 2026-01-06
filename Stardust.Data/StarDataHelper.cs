using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NewLife;
using NewLife.Log;
using Stardust.Data.Monitors;
using XCode.DataAccessLayer;
using XCode.Shards;

namespace Stardust.Data;

/// <summary>星尘数据助手</summary>
public static class StarDataHelper
{
    /// <summary>在Sqlite中拆分TraceData和SampleData到多个Sqlite数据库</summary>
    public static void SplitSqliteTables()
    {
        var dal = DAL.Create("StardustData");
        if (dal.DbType != DatabaseType.SQLite) return;

        // 调整分表逻辑
        if (TraceData.Meta.ShardPolicy is TimeShardPolicy tsp1)
            tsp1.ConnPolicy = "Trace{1:dd}";
        if (SampleData.Meta.ShardPolicy is TimeShardPolicy tsp2)
            tsp2.ConnPolicy = "Trace{1:dd}";

        var builder = new ConnectionStringBuilder(dal.ConnStr);
        var dbfile = builder["Data Source"];
        if (dbfile.IsNullOrEmpty()) return;

        // 判断Traces目录是否存在，存在则表示已经拆分过
        var p = dbfile.LastIndexOfAny('/', '\\');
        var dir = p >= 0 ? dbfile[..p] : ".";
        dir = dir + "/Traces";
        //if (Directory.Exists(dir.GetFullPath())) return;
        var existed = Directory.Exists(dir.GetFullPath());

        // 如果已存在，不要显示太多日志
        using var show = dal.Session.SetShowSql(!existed);
        //DAL.Debug = !existed;

        var tables = dal.Tables;
        var tnames = tables.Select(e => e.TableName).ToArray();
        var tableTrace = TraceData.Meta.Table.DataTable;
        var tableSample = SampleData.Meta.Table.DataTable;

        // 添加31个分库连接字符串
        for (var i = 0; i < 31; i++)
        {
            var date = i + 1;
            var key = $"Trace{date:00}";
            var file = $"{dir}/{key}.db";
            if (!DAL.ConnStrs.ContainsKey(key))
            {
                builder["Data Source"] = file;
                DAL.AddConnStr(key, builder.ToString(), null, "sqlite");

                if (!File.Exists(file))
                {
                    var table1 = tableTrace.Clone() as IDataTable;
                    table1.TableName = $"TraceData_{date:00}";

                    var table2 = tableSample.Clone() as IDataTable;
                    table2.TableName = $"SampleData_{date:00}";

                    DAL.Create(key).Db.CreateMetaData().SetTables(Migration.On, [table1, table2]);
                }
            }

            //key = $"SampleData{date:00}";
            //file = $"{dir}/{key}.db";
            //if (!DAL.ConnStrs.ContainsKey(key))
            //{
            //    builder["Data Source"] = file;
            //    DAL.AddConnStr(key, builder.ToString(), null, "sqlite");

            //    if (!File.Exists(file))
            //    {
            //        var table = tableSample.Clone() as IDataTable;
            //        table.TableName = $"SampleData_{date:00}";
            //        DAL.Create(key).Db.CreateMetaData().SetTables(Migration.On, table);
            //    }
            //}
        }

        // 准备迁移表数据，并在原始库中删除表
        var success = 0;
        for (var i = 0; i < 31; i++)
        {
            var date = i + 1;
            var key = $"Trace{date:00}";
            var name = $"TraceData_{date:00}";
            var file = $"{dir}/{key}.db".GetFullPath();
            if (name.EqualIgnoreCase(tnames))
            {
                // 迁移数据
                XTrace.WriteLine("迁移表[{0}]到数据库[{1}]，文件：{2}", name, key, file);

                var sql = $"ATTACH DATABASE '{file}' AS target;\r\nINSERT INTO target.TraceData_{date:00} SELECT * FROM main.TraceData_{date:00};\r\nDETACH DATABASE target;DROP TABLE main.TraceData_{date:00};";
                success += dal.Execute(sql);
            }

            //key = $"SampleData{date:00}";
            name = $"SampleData_{date:00}";
            //file = $"{dir}/{key}.db".GetFullPath();
            if (name.EqualIgnoreCase(tnames))
            {
                // 迁移数据
                XTrace.WriteLine("迁移表[{0}]到数据库[{1}]，文件：{2}", name, key, file);

                var sql = $"ATTACH DATABASE '{file}' AS target;\r\nINSERT INTO target.SampleData_{date:00} SELECT * FROM main.SampleData_{date:00};\r\nDETACH DATABASE target;DROP TABLE main.SampleData_{date:00};";
                success += dal.Execute(sql);
            }
        }

        // 压缩StardustData库
        if (success > 0) dal.Execute("VACUUM");
    }
}
