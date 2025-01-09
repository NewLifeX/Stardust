using System;
using NewLife;

namespace Stardust.Data.Monitors;

/// <summary>跟踪统计模型</summary>
public class TraceStatModel
{
    /// <summary>时间</summary>
    public DateTime Time { get; set; }

    /// <summary>应用</summary>
    public Int32 AppId { get; set; }

    ///// <summary>操作名</summary>
    //public String Name { get; set; }

    /// <summary>跟踪项</summary>
    public Int32 ItemId { get; set; }

    /// <summary>用于统计的唯一Key</summary>
    public String Key => $"{Time.ToFullString()}#{AppId}#{ItemId}";

    /// <summary>已重载。用于统计的唯一Key</summary>
    /// <returns></returns>
    public override String ToString() => $"{Time.ToFullString()}#{AppId}#{ItemId}";
}