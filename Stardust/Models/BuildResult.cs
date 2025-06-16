namespace Stardust.Models;

/// <summary>构建结果</summary>
public class BuildResult
{
    /// <summary>构建任务ID</summary>
    public Int32 Id { get; set; }

    /// <summary>提交标识</summary>
    public String? CommitId { get; set; }

    /// <summary>提交记录</summary>
    public String? CommitLog { get; set; }

    /// <summary>提交时间</summary>
    public DateTime CommitTime { get; set; }

    /// <summary>发布进度</summary>
    public String? Progress { get; set; }
}
