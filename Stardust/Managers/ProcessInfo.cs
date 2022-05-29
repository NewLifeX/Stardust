using System.Diagnostics;
using NewLife.IO;

namespace Stardust.Managers;

/// <summary>服务运行信息</summary>
internal class ProcessInfo
{
    public String Name { get; set; }

    public Int32 ProcessId { get; set; }

    public String ProcessName { get; set; }

    public DateTime CreateTime { get; set; }

    public DateTime UpdateTime { get; set; }
}