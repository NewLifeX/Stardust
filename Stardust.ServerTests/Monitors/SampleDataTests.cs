using System;
using System.Collections.Generic;
using NewLife.Log;
using Stardust.Data;
using Stardust.Data.Monitors;
using Xunit;

namespace ServerTest.Monitors;

/// <summary>采样数据单元测试。覆盖 SampleData.Create 的跨度映射与空集合防御</summary>
public class SampleDataTests
{
    [Fact]
    public void Create_MapsSpanFields()
    {
        var data = new TraceData
        {
            AppId = 998002,
            ItemId = 888002,
            Name = "test-sample-" + Guid.NewGuid().ToString("n")[..8],
            ClientId = "sample-client",
        };

        var tracer = new DefaultTracer { Period = 0 };
        var span = tracer.NewSpan("sample-op");
        span.SetTag("v");

        var list = SampleData.Create(data, [span], true);

        Assert.Single(list);
        var sd = list[0];
        Assert.Equal(data.Id, sd.DataId);
        Assert.Equal(data.ItemId, sd.ItemId);
        Assert.Equal(data.ClientId, sd.ClientId);
        Assert.Equal(span.Id, sd.SpanId);
        Assert.Equal(span.TraceId, sd.TraceId);
        Assert.Equal(span.ParentId, sd.ParentId);
        Assert.Equal(span.Value, sd.Value);
        Assert.Equal(span.Tag, sd.Tag);
        Assert.Equal(span.Error, sd.Error);
        Assert.Equal(span.StartTime, sd.StartTime);
        Assert.True(sd.Success);
        Assert.True(sd.Id > 0);
    }

    [Fact]
    public void Create_EmptySpans_ReturnsEmptyList()
    {
        var data = new TraceData { AppId = 998003, ItemId = 888003, Name = "test-sample-empty" };

        Assert.Empty(SampleData.Create(data, null, true));
        Assert.Empty(SampleData.Create(data, new List<ISpan>(), false));
    }
}
