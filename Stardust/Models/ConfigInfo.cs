namespace Stardust.Models;

/// <summary>配置信息</summary>
public class ConfigInfo
{
    /// <summary>版本</summary>
    public Int32 Version { get; set; }

    /// <summary>作用域。dev/test/stag/pro</summary>
    public String? Scope { get; set; }

    /// <summary>来源IP地址</summary>
    public String? SourceIP { get; set; }

    /// <summary>下一个版本。如果不同于的当前版本，则说明有新版本等待发布</summary>
    public Int32 NextVersion { get; set; }

    /// <summary>下次发布时间。用于定时发布</summary>
    public String? NextPublish { get; set; }

    /// <summary>更新时间</summary>
    public DateTime UpdateTime { get; set; }

    /// <summary>配置项集合</summary>
    public IDictionary<String, String>? Configs { get; set; }
}