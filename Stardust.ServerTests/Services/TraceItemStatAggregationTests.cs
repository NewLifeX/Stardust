using System;
using System.Linq;
using NewLife.Security;
using Stardust.Data.Monitors;
using Stardust.Server.Services;
using Xunit;

namespace ServerTest.Services;

/// <summary>埋点项统计的聚合测试。覆盖按应用把每日统计汇总回写到跟踪项（APM-5）</summary>
public class TraceItemStatAggregationTests
{
    [Fact(DisplayName = "按应用聚合每日统计并回写跟踪项的次数、错误与平均耗时")]
    public void Process_AggregatesDayStat()
    {
        var appId = Math.Abs(Guid.NewGuid().GetHashCode()) % 800000 + 100000;
        var name = "api_" + Rand.NextString(6);

        var item = new TraceItem { AppId = appId, Name = name, Kind = "Api", Enable = true };
        item.Insert();

        var d1 = new TraceDayStat { AppId = appId, ItemId = item.Id, Name = name, StatDate = DateTime.Today, Total = 5, Errors = 1, Cost = 100, Type = "Api" };
        d1.Insert();
        var d2 = new TraceDayStat { AppId = appId, ItemId = item.Id, Name = name, StatDate = DateTime.Today.AddDays(-1), Total = 7, Errors = 2, Cost = 200, Type = "Api" };
        d2.Insert();

        try
        {
            new TraceItemStatService(null).Process(appId, 7);

            var saved = TraceItem.FindById(item.Id);
            Assert.NotNull(saved);
            Assert.Equal(12, saved.Total);
            Assert.Equal(3, saved.Errors);
            // 跟踪项的 Cost 是平均耗时（两天分别是 100 与 200）
            Assert.Equal(150, saved.Cost);
        }
        finally
        {
            d1.Delete();
            d2.Delete();
            TraceItem.FindById(item.Id)?.Delete();
        }
    }

    [Fact(DisplayName = "没有每日统计时处理不改变跟踪项数据")]
    public void Process_NoDayStat_KeepsItem()
    {
        var appId = Math.Abs(Guid.NewGuid().GetHashCode()) % 800000 + 100000;
        var item = new TraceItem { AppId = appId, Name = "api_" + Rand.NextString(6), Kind = "Api", Enable = true };
        item.Insert();

        try
        {
            new TraceItemStatService(null).Process(appId, 7);

            var saved = TraceItem.FindById(item.Id);
            Assert.NotNull(saved);
            Assert.Equal(0, saved.Total);
        }
        finally
        {
            TraceItem.FindById(item.Id)?.Delete();
        }
    }
}
