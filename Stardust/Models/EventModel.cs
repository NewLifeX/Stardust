namespace Stardust.Models;

/// <summary>事件模型</summary>
public class EventModel
{
    /// <summary>时间。Unix毫秒，UTC</summary>
    public Int64 Time { get; set; }

    /// <summary>事件类型。info/alert/error</summary>
    public String? Type { get; set; }

    /// <summary>名称。事件名称，例如LightOpen</summary>
    public String? Name { get; set; }

    /// <summary>内容。事件详情</summary>
    public String? Remark { get; set; }
}