#if NET5_0_OR_GREATER
using System;
using System.Data.Common;
using System.Text.RegularExpressions;
using NewLife;
using NewLife.Log;
using NewLife.Reflection;

namespace Stardust.Monitors;

/// <summary>EfCore诊断监听器</summary>
public class EfCoreDiagnosticListener : TraceDiagnosticListener
{
    /// <summary>实例化</summary>
    public EfCoreDiagnosticListener() => Name = "Microsoft.EntityFrameworkCore";

    /// <summary>下一步</summary>
    /// <param name="value"></param>
    public override void OnNext(KeyValuePair<String, Object?> value)
    {
        if (Tracer == null) return;

        // 前缀可能是 Microsoft.EntityFrameworkCore.Database.Command.
        var name = value.Key.Split(".").LastOrDefault();

        var span = DefaultSpan.Current;
        var spanName = (span as DefaultSpan)?.Builder?.Name;

        switch (name)
        {
            case "CommandExecuting":
                {
                    if (value.Value != null && value.Value.GetValue("Command") is DbCommand command)
                    {
                        var sql = command.CommandText;

                        // 从sql解析表名，作为跟踪名一部分。正则避免from前后换行的情况
                        var action = "";
                        if (sql.StartsWithIgnoreCase("Insert ", "Update ", "Delete ", "Upsert "))
                        {
                            // 使用 Insert/Update/Delete 作为埋点操作名
                            var p = sql.IndexOf(' ');
                            if (p > 0) action = sql[..p];
                        }
                        else if (sql.StartsWithIgnoreCase("Select Count"))
                        {
                            action = "SelectCount";
                        }
                        else if (sql.StartsWithIgnoreCase("Select "))
                        {
                            // 查询数据时，Group作为独立埋点操作名
                            if (sql.Contains("group by", StringComparison.CurrentCultureIgnoreCase))
                                action = "Group";
                        }

                        var dbName = command.Connection?.Database;
                        var traceName = $"db:{dbName}:{action}";

                        var tables = GetTables(sql, true);
                        if (tables.Length > 0) traceName += ":" + tables.Join("-");

                        Tracer.NewSpan(traceName, sql);
                    }

                    break;
                }
            case "CommandExecuted":
                {
                    if (span != null && !spanName.IsNullOrEmpty() && spanName.StartsWith("db:"))
                    {
                        span.Dispose();
                    }

                    break;
                }

            case "CommandError":
                {
                    if (span != null && !spanName.IsNullOrEmpty() && spanName.StartsWith("db:"))
                    {
                        if (value.Value != null && value.Value.GetValue("Exception") is Exception ex) span.SetError(ex, null);

                        span.Dispose();
                    }
                    break;
                }
        }
    }

    private static readonly Regex reg_table = new("(?:\\s+from|insert\\s+into|update|\\s+join|drop\\s+table|truncate\\s+table)\\s+[`'\"\\[]?([\\w]+)[`'\"\\[]?", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    /// <summary>从Sql语句中截取表名</summary>
    /// <param name="sql">Sql语句</param>
    /// <param name="trimShard">是否去掉表名后面的分表信息。如日期分表</param>
    /// <returns></returns>
    public static String[] GetTables(String sql, Boolean trimShard)
    {
        var list = new List<String>();
        var ms = reg_table.Matches(sql);
        foreach (Match item in ms)
        {
            //list.Add(item.Groups[1].Value);
            var tableName = item.Groups[1].Value;
            if (trimShard && tableName.Contains("_"))
            {
                var p = tableName.LastIndexOf('_');
                if (p > 0 && tableName.Substring(p + 1).ToInt() > 0)
                {
                    tableName = tableName.Substring(0, p);
                }
            }
            if (!list.Contains(tableName)) list.Add(tableName);
        }
        return list.ToArray();
    }
}
#endif