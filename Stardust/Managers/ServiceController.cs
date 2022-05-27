using System.Diagnostics;
using NewLife;
using NewLife.IO;
using Stardust.Models;

namespace Stardust.Managers;

/// <summary>
/// 应用服务控制器
/// </summary>
internal class ServiceController
{
    #region 属性
    /// <summary>服务名</summary>
    public String Name { get; set; }

    /// <summary>进程ID</summary>
    public Int32 ProcessId { get; set; }

    /// <summary>进程名</summary>
    public String ProcessName { get; set; }

    /// <summary>服务信息</summary>
    public ServiceInfo Info { get; set; }

    /// <summary>进程</summary>
    public Process Process { get; set; }
    #endregion

    #region 方法
    public void SetProcess(Process process)
    {
        Process = process;
        if (process != null)
        {
            ProcessId = process.Id;
            ProcessName = process.ProcessName;
        }
        else
        {
            ProcessId = 0;
            ProcessName = null;
        }
    }

    public void Save(CsvDb<ProcessInfo> db)
    {
        var pi = db.Find(e => e.Name.EqualIgnoreCase(Name));
        if (pi == null) pi = new ProcessInfo { Name = Name };

        pi.Save(db, Process);
    }
    #endregion
}