namespace Stardust.Models;

/// <summary>构建任务</summary>
public class BuildTask
{
    /// <summary>构建任务ID</summary>
    public Int32 Id { get; set; }

    /// <summary>应用ID</summary>
    public String Name { get; set; } = String.Empty;
}
