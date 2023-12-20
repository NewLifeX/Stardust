using System.ComponentModel;

namespace Stardust.Models;

/// <summary>迁移服务器事件参数</summary>
public class MigrationEventArgs : CancelEventArgs
{
    /// <summary>新服务器</summary>
    public String? NewServer { get; set; }
}
