namespace Stardust.Web.Areas.Monitors;

/// <summary>监控查询辅助</summary>
public static class MonitorSearchHelper
{
    /// <summary>解析列表查询的时间范围</summary>
    /// <remarks>
    /// 未选择时间时，选中应用按统计时间展示趋势；列表页默认只查当天。
    /// 历史数据量极大，全表扫描既慢，又可能因早期异常值导致读取溢出。
    /// </remarks>
    /// <param name="appId">应用编号，-1表示全部</param>
    /// <param name="itemId">埋点编号，-1表示全部</param>
    /// <param name="date">统计日期</param>
    /// <param name="start">开始时间</param>
    /// <param name="end">结束时间</param>
    public static void ResolveDateRange(Int32 appId, Int32 itemId, DateTime date, ref DateTime start, ref DateTime end)
    {
        if (start.Year >= 2000 || end.Year >= 2000) return;

        if (date.Year > 2000)
            start = end = date;
        else if (appId < 0 && itemId < 0)
            start = end = DateTime.Today;
    }
}
