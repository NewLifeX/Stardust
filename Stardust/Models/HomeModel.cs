namespace Stardust.Models;

/// <summary>星尘平台首页概览。服务端首页展示的节点、应用与调用统计</summary>
/// <remarks>数据来自会话表与预聚合表，均为只读统计，不涉及明细</remarks>
public class HomeSummary
{
    /// <summary>服务端地址</summary>
    public String? Server { get; set; }

    /// <summary>管理控制台地址。StarWeb 地址，为空表示未部署</summary>
    public String? WebUrl { get; set; }

    /// <summary>展示模式。hidden/magnitude/exact，由服务端配置决定</summary>
    public String? Mode { get; set; }

    /// <summary>服务端版本</summary>
    public String? Version { get; set; }

    /// <summary>已运行秒数</summary>
    public Int64 Uptime { get; set; }

    /// <summary>在线节点数</summary>
    public Int32 NodeOnline { get; set; }

    /// <summary>节点总数</summary>
    public Int32 NodeTotal { get; set; }

    /// <summary>在线应用数</summary>
    public Int32 AppOnline { get; set; }

    /// <summary>应用总数</summary>
    public Int32 AppTotal { get; set; }

    /// <summary>今日调用量</summary>
    public Int64 TodayCall { get; set; }

    /// <summary>昨日调用量。用于展示环比趋势</summary>
    public Int64 YesterdayCall { get; set; }

    /// <summary>今日错误数</summary>
    public Int64 TodayError { get; set; }

    /// <summary>今日错误率。0~1</summary>
    public Double ErrorRate { get; set; }
}
