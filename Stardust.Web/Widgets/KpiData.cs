namespace Stardust.Web.Widgets;

/// <summary>KPI 指标数据。魔方工作台 KPI 卡数据契约 { Value, Trend, Url }，
/// 公开类型 + 公开属性，供跨程序集动态绑定渲染（业务程序集不能返回匿名类型）</summary>
public class KpiData
{
    /// <summary>主数值</summary>
    public String Value { get; set; }

    /// <summary>趋势描述</summary>
    public String Trend { get; set; }

    /// <summary>跳转地址</summary>
    public String Url { get; set; }
}
