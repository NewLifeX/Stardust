using System;
using Stardust.Data.Monitors;
using Xunit;

namespace ServerTest.Mcp;

/// <summary>告警处置只有一份实现：实体追加意见与结束时间，不改动作和报警状态。</summary>
public class AlarmToolsReuseTests
{
    [Fact(DisplayName = "处置告警：保留动作，写入意见与结束时间")]
    public void Resolve_WritesActionContentAndEndTime()
    {
        var record = new AlarmRecord
        {
            Name = "alarm-resolve-" + Guid.NewGuid().ToString("n")[..8],
            Category = "测试告警",
            Action = "测试对象",
            Content = "原始内容",
            Status = AlarmStatuses.Alarming,
            StartTime = DateTime.Now.AddMinutes(-5),
        };
        record.Insert();

        try
        {
            var before = DateTime.Now.AddSeconds(-2);
            Assert.True(record.Resolve("已处理"));

            var found = AlarmRecord.FindById(record.Id);
            Assert.NotNull(found);
            Assert.Equal("测试对象", found.Action);
            Assert.Equal("原始内容\nMCP处置：已处理", found.Content);
            Assert.True(found.EndTime >= before);
            Assert.Equal(AlarmStatuses.Alarming, found.Status);
        }
        finally
        {
            record.Delete();
        }
    }
}
