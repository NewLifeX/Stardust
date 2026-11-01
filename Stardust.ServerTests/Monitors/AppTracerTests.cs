using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using NewLife.Security;
using Stardust.Data;
using Stardust.Data.Monitors;
using Xunit;

namespace ServerTest.Monitors;

/// <summary>应用跟踪器测试</summary>
public class AppTracerTests
{
    [Fact]
    [DisplayName("并发创建同一跟踪项不会抛唯一索引异常")]
    public async Task GetOrAddItem_Concurrent_ReturnsSameItem()
    {
        var name = "tga_" + Rand.NextString(8);
        var app = new App { Name = name, Secret = "tga-secret", Enable = true };
        app.Insert();
        var tracer = new AppTracer { Name = name, AppId = app.Id, Enable = true };
        tracer.Insert();

        var itemName = "/api/concurrent-" + Rand.NextString(6);
        try
        {
            var tasks = Enumerable.Range(0, 8).Select(_ => Task.Run(() => tracer.GetOrAddItem(itemName))).ToArray();
            var items = await Task.WhenAll(tasks);

            Assert.All(items, e => Assert.NotNull(e));
            Assert.Single(items.Select(e => e.Id).Distinct());
            Assert.Single(TraceItem.FindAllByApp(tracer.ID), e => e.Name == itemName);
        }
        finally
        {
            foreach (var item in TraceItem.FindAllByApp(tracer.ID))
            {
                item.Delete();
            }
            tracer.Delete();
            app.Delete();
        }
    }
}
