using Stardust.Data;
using Stardust.Data.Monitors;
using Xunit;

namespace ServerTest.Monitors;

/// <summary>报警确认与静默测试。覆盖字段落库、通知判定与静默期抑制</summary>
public class AlarmLifecycleTests
{
    private static String NewName() => "alarm-life-" + Guid.NewGuid().ToString("n")[..8];

    [Fact(DisplayName = "确认报警写入确认人与时间")]
    public void Ack_WritesUserAndTime()
    {
        var record = new AlarmRecord
        {
            Name = NewName(),
            Category = "测试告警",
            Action = "测试对象",
            Status = AlarmStatuses.Alarming,
            StartTime = DateTime.Now,
        };

        try
        {
            record.Insert();
            Assert.True(record.Ack("tester"));

            var found = AlarmRecord.FindById(record.Id);
            Assert.NotNull(found);
            Assert.True(found.IsAcked);
            Assert.Equal("tester", found.AckUser);
            Assert.True(found.AckTime.Year > 2000);
        }
        finally
        {
            record.Delete();
        }
    }

    [Fact(DisplayName = "静默报警写入截止时间并抑制通知")]
    public void Silence_SuppressesNotifyUntilExpired()
    {
        var record = new AlarmRecord
        {
            Name = NewName(),
            Category = "测试告警",
            Action = "测试对象",
            Status = AlarmStatuses.Alarming,
            StartTime = DateTime.Now,
        };

        try
        {
            record.Insert();
            Assert.True(record.CanNotify);

            Assert.True(record.Silence(1));
            Assert.True(record.IsSilenced);
            Assert.False(record.CanNotify);

            // 截止时间已过，恢复通知
            record.SilenceTime = DateTime.Now.AddMinutes(-1);
            Assert.True(record.CanNotify);

            // 已恢复的报警不再通知
            record.Status = AlarmStatuses.Recovered;
            Assert.False(record.CanNotify);
        }
        finally
        {
            record.Delete();
        }
    }

    [Fact(DisplayName = "静默期内发送报警不增加通知次数")]
    public void SendAlarmWithRecord_Silenced_DoesNotIncreaseTimes()
    {
        var category = NewName();
        var action = "测试对象";
        var record = new AlarmRecord
        {
            Name = $"{category}-{action}",
            Category = category,
            Action = action,
            Status = AlarmStatuses.Alarming,
            StartTime = DateTime.Now,
        };

        try
        {
            record.Insert();
            record.Silence(1);

            RobotHelper.SendAlarmWithRecord("测试组", null, "测试告警", "测试内容", category, action);
            var found = AlarmRecord.FindById(record.Id);
            Assert.NotNull(found);
            Assert.Equal(0, found.Times);

            // 静默失效后再次发送，通知次数增加
            found.SilenceTime = DateTime.Now.AddMinutes(-1);
            found.Update();

            RobotHelper.SendAlarmWithRecord("测试组", null, "测试告警", "测试内容", category, action);
            found = AlarmRecord.FindById(record.Id);
            Assert.NotNull(found);
            Assert.True(found.Times > 0);
        }
        finally
        {
            record.Delete();
        }
    }
}
