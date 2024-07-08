namespace Stardust.Managers;

/// <summary>服务运行信息</summary>
public class ProcessInfo
{
    /// <summary>名称</summary>
    public String Name { get; set; } = null!;

    /// <summary>进程Id</summary>
    public Int32 ProcessId { get; set; }

    /// <summary>进程名</summary>
    public String? ProcessName { get; set; }

    /// <summary>创建时间</summary>
    public DateTime CreateTime { get; set; }

    /// <summary>更新时间</summary>
    public DateTime UpdateTime { get; set; }
}