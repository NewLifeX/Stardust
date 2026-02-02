using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NewLife;
using NewLife.Log;
using Stardust.Data.Monitors;
using Stardust.Data.Nodes;
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
        var root = p >= 0 ? dbfile[..p] : ".";
        var dir = root + "/Traces";
        var existed = Directory.Exists(dir.GetFullPath());

        // 如果已存在，不要显示太多日志
        using var show = dal.Session.SetShowSql(!existed);
        //DAL.Debug = !existed;

        var tables = dal.Tables;
        var tnames = tables.Select(e => e.TableName).ToArray();
        var tableTrace = TraceData.Meta.Table.DataTable;
        var tableSample = SampleData.Meta.Table.DataTable;

        // 添加31个分库连接字符串，同一天的TraceData表和SampleData表必须在同一个库，有关联查询
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

                var sql = $"""
                    ATTACH DATABASE '{file}' AS target;
                    INSERT INTO target.{name} SELECT * FROM main.{name};
                    DETACH DATABASE target;
                    DROP TABLE main.{name};
                    """;
                success += dal.Execute(sql);
            }

            //key = $"SampleData{date:00}";
            name = $"SampleData_{date:00}";
            //file = $"{dir}/{key}.db".GetFullPath();
            if (name.EqualIgnoreCase(tnames))
            {
                // 迁移数据
                XTrace.WriteLine("迁移表[{0}]到数据库[{1}]，文件：{2}", name, key, file);

                var sql = $"""
                    ATTACH DATABASE '{file}' AS target;
                    INSERT INTO target.{name} SELECT * FROM main.{name};
                    DETACH DATABASE target;
                    DROP TABLE main.{name};
                    """;
                success += dal.Execute(sql);
            }
        }

        // 准备拆分频繁写入的表，避免数据库文件过大
        var targets = new[] { AppMinuteStat.Meta.Factory, TraceMinuteStat.Meta.Factory, TraceHourStat.Meta.Factory, NodeData.Meta.Factory, NodeHistory.Meta.Factory, AppMeter.Meta.Factory, AppHistory.Meta.Factory };
        foreach (var factory in targets)
        {
            var key = factory.TableName;
            var file = $"{root}/{key}.db";
            if (!DAL.ConnStrs.ContainsKey(key))
            {
                builder["Data Source"] = file;
                DAL.AddConnStr(key, builder.ToString(), null, "sqlite");

                if (!File.Exists(file))
                {
                    var table1 = factory.Table.DataTable.Clone() as IDataTable;

                    factory.ConnName = key;
                    factory.Table.ConnName = key;

                    DAL.Create(key).Db.CreateMetaData().SetTables(Migration.On, [table1]);
                }
            }
        }
        foreach (var factory in targets)
        {
            var key = factory.TableName;
            var name = factory.TableName;
            var file = $"{root}/{key}.db".GetFullPath();
            if (name.EqualIgnoreCase(tnames))
            {
                // 迁移数据
                XTrace.WriteLine("迁移表[{0}]到数据库[{1}]，文件：{2}", name, key, file);

                var oldTable = tables.FirstOrDefault(e => e.TableName.EqualIgnoreCase(name));
                var insertSql = BuildSafeInsertSql(factory.Table.DataTable, oldTable, "main", "target");
                if (insertSql.IsNullOrEmpty())
                {
                    XTrace.WriteLine("跳过表[{0}]的迁移", name);
                    continue;
                }

                var sql = $"""
                    ATTACH DATABASE '{file}' AS target;
                    {insertSql};
                    DETACH DATABASE target;
                    DROP TABLE main.{name};
                    """;
                success += dal.Execute(sql);
            }
        }

        // 压缩StardustData库
        if (success > 0) dal.Execute("VACUUM");
    }

    /// <summary>构建安全的INSERT语句，处理不可空字段的空值问题和新旧表字段不一致问题</summary>
    /// <param name="newTable">新表信息（目标表结构）</param>
    /// <param name="oldTable">旧表信息（源表结构），如果为null则使用新表结构</param>
    /// <param name="sourceSchema">源数据库架构名称（如 main）</param>
    /// <param name="targetSchema">目标数据库架构名称（如 target）</param>
    /// <returns>安全的INSERT语句</returns>
    private static String BuildSafeInsertSql(IDataTable newTable, IDataTable oldTable, String sourceSchema, String targetSchema)
    {
        var newColumns = newTable.Columns;
        if (newColumns == null || newColumns.Count == 0)
            return $"INSERT INTO {targetSchema}.{newTable.TableName} SELECT * FROM {sourceSchema}.{oldTable?.TableName ?? newTable.TableName}";

        // 获取旧表的字段名集合（用于判断字段是否存在）
        var oldColumnNames = oldTable?.Columns?.Select(c => c.ColumnName).ToHashSet(StringComparer.OrdinalIgnoreCase) ?? [];

        var fieldNames = new List<String>();
        var selectFields = new List<String>();

        foreach (var column in newColumns)
        {
            // 如果指定了旧表，只处理旧表中也存在的字段
            if (oldTable != null && oldColumnNames.Count > 0 && !oldColumnNames.Contains(column.ColumnName))
            {
                // 新表有但旧表没有的字段，跳过
                continue;
            }

            fieldNames.Add(column.ColumnName);

            // 如果字段不可空，使用 IFNULL 处理
            if (!column.Nullable)
            {
                var defaultValue = GetDefaultValue(column);
                selectFields.Add($"IFNULL({column.ColumnName}, {defaultValue})");
            }
            else
            {
                selectFields.Add(column.ColumnName);
            }
        }

        // 如果没有公共字段，返回空操作（避免SQL错误）
        if (fieldNames.Count == 0)
        {
            XTrace.WriteLine("警告：表[{0}]在新旧结构中没有公共字段，跳过迁移", newTable.TableName);
            return null;
        }

        var fields = String.Join(", ", fieldNames);
        var selects = String.Join(", ", selectFields);
        var sourceTableName = oldTable?.TableName ?? newTable.TableName;

        return $"INSERT INTO {targetSchema}.{newTable.TableName} ({fields}) SELECT {selects} FROM {sourceSchema}.{sourceTableName}";
    }

    /// <summary>根据字段类型获取默认值</summary>
    /// <param name="column">字段信息</param>
    /// <returns>默认值字符串</returns>
    private static String GetDefaultValue(IDataColumn column)
    {
        // 根据字段类型返回适当的默认值
        return column.DataType?.Name switch
        {
            nameof(Int16) or nameof(Int32) or nameof(Int64) or
            nameof(UInt16) or nameof(UInt32) or nameof(UInt64) or
            nameof(Byte) or nameof(SByte) => "0",

            nameof(Single) or nameof(Double) or nameof(Decimal) => "0.0",

            nameof(Boolean) => "0",

            nameof(DateTime) => "'1970-01-01 00:00:00'",

            nameof(String) => "''",

            _ => "''"
        };
    }
}
