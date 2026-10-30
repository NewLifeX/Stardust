namespace Stardust.Web.Areas.Monitors;

/// <summary>监控查询辅助</summary>
public static class MonitorSearchHelper
{
    /// <summary>是否需要列表合计行</summary>
    /// <remarks>
    /// 合计行会对查询范围执行 SUM(Total/TotalCost/TotalValue)。跨应用且时间跨度大时，MySQL 的 SUM 返回 DECIMAL，
    /// 累计值一旦超出 Int64 范围，XCode 在装回实体时抛 OverflowException，整个列表页 500。
    /// 统计表的合计行只在选定单个应用时才有意义，因此限定应用后再开启。
    /// </remarks>
    /// <param name="appId">应用编号，-1表示全部</param>
    /// <returns>是否需要合计行</returns>
    public static Boolean NeedState(Int32 appId) => appId > 0;

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
