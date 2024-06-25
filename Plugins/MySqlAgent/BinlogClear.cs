using NewLife;
using NewLife.Log;
using NewLife.Remoting.Clients;
using NewLife.Threading;

namespace MySqlAgent;

internal class BinlogClear
{
    public IEventProvider Event { get; set; }

    private TimerX _timer;
    public void Start()
    {
        Event?.WriteInfoEvent("Binlog", "启动二进制日志清理");

        _timer = new TimerX(DoClear, null, 1000, 3600_000);
    }

    public void Stop()
    {
        _timer.TryDispose();
    }

    private void DoClear(Object? state)
    {
        try
        {
            //var dir = "C:/ProgramData/MySQL/MySQL Server 8.0/Data";
            //if (!Directory.Exists(dir)) dir = "/var/lib/mysql";
            var dir = "/var/lib/mysql";
            if (!Directory.Exists(dir)) return;

            var logs = Directory.GetFiles(dir, "binlog.*");
            if (logs == null || logs.Length == 0) return;

            // 删除最近1小时之前的日志文件
            var exp = DateTime.Now.AddHours(-1);
            foreach (var item in logs)
            {
                var fi = new FileInfo(item);
                if (fi.Extension.TrimStart('.').ToInt() > 0 && fi.LastWriteTime < exp)
                {
                    Event?.WriteInfoEvent("Binlog", $"删除二进制日志 {fi.Name}");
                    XTrace.WriteLine("删除二进制日志 {0}", fi.Name);
                    try
                    {
                        fi.Delete();
                    }
                    catch (Exception ex)
                    {
                        Event?.WriteErrorEvent("Binlog", ex.Message);
                        XTrace.WriteException(ex);
                    }
                }
            }

            //var sql = new StringBuilder();
            //sql.AppendFormat("PURGE BINARY LOGS TO '{0}'", log);
            //sql.AppendLine(";");

            //var rs = MySqlHelper.Execute(sql.ToString());
            //if (rs > 0) XTrace.WriteLine("清理二进制日志 {0} 成功", log);
        }
        catch (Exception ex)
        {
            Event?.WriteErrorEvent("DeleteBinlog", ex.Message);
            XTrace.WriteException(ex);
        }
    }
}
