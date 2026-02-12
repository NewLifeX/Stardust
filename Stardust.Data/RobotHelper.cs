using System;
using NewLife;
using NewLife.Log;
using Stardust.Data.Monitors;
using Stardust.Data.Platform;
using Stardust.DingTalk;
using Stardust.WeiXin;

namespace Stardust.Data;

/// <summary>告警机器人</summary>
public class RobotHelper
{
    /// <summary>是否能够告警</summary>
    /// <param name="groupName"></param>
    /// <param name="webhook"></param>
    /// <returns></returns>
    public static Boolean CanAlarm(String groupName, String webhook)
    {
        if (!webhook.IsNullOrEmpty()) return true;

        var group = AlarmGroup.FindByName(groupName);
        return group != null && group.Enable && !group.WebHook.IsNullOrEmpty();
    }

    /// <summary>获取告警机器人链接</summary>
    /// <param name="groupName"></param>
    /// <param name="webhook"></param>
    /// <returns></returns>
    public static String GetAlarm(GalaxyProject project, String groupName, String webhook)
    {
        if (!webhook.IsNullOrEmpty()) return webhook;

        var group = AlarmGroup.FindByName(groupName);
        if (group != null && group.Enable && !group.WebHook.IsNullOrEmpty()) return group.WebHook;

        if (project != null)
        {
            group = AlarmGroup.FindByName(project.Name);
            if (group != null && group.Enable && !group.WebHook.IsNullOrEmpty()) return group.WebHook;
        }

        return null;
    }

    /// <summary>发送告警</summary>
    /// <param name="groupName">告警组</param>
    /// <param name="webhook">告警Url</param>
    /// <param name="title">告警标题</param>
    /// <param name="message">告警消息</param>
    public static AlarmHistory SendAlarm(String groupName, String webhook, String title, String message)
    {
        using var span = DefaultTracer.Instance?.NewSpan("alarm:SendAlarm", new { groupName, webhook, title });

        XTrace.WriteLine(message);

        var group = AlarmGroup.FindByName(groupName);

        var hi = new AlarmHistory
        {
            GroupId = group?.Id ?? 0,
            Name = groupName,
            Success = true,
            Action = title,
            Content = message,

            Creator = Environment.MachineName,
        };

        try
        {
            if (webhook.IsNullOrEmpty() && group != null && group.Enable) webhook = group?.WebHook;
            //if (webhook.IsNullOrEmpty()) throw new InvalidOperationException("未设置或未启用告警地址！");
            if (webhook.IsNullOrEmpty()) return hi;

            if (webhook.Contains("qyapi.weixin"))
            {
                hi.Category = "QyWeixin";

                var weixin = new WeiXinClient { Url = webhook };

                using var span2 = DefaultTracer.Instance?.NewSpan("SendWeixin", message);
                weixin.SendMarkDown(message);
            }
            else if (webhook.Contains("dingtalk"))
            {
                hi.Category = "DingTalk";

                var dingTalk = new DingTalkClient { Url = webhook };
                message = dingTalk.FormatMarkdown(message);
                hi.Content = message;

                using var span2 = DefaultTracer.Instance?.NewSpan("SendDingTalk", message);
                dingTalk.SendMarkDown(title, message, null);
            }
        }
        catch (Exception ex)
        {
            span?.SetError(ex, null);

            hi.Success = false;
            hi.Error = ex.ToString();
        }

        //hi.SaveAsync();
        hi.Insert();

        return hi;
    }

    /// <summary>获取或创建报警记录。用于管理报警的生命周期</summary>
    /// <param name="category">类别。如应用下线、节点下线、CPU告警等</param>
    /// <param name="action">操作。报警对象，如应用名或节点名</param>
    /// <param name="groupName">告警组名</param>
    /// <returns>报警记录。如果已有活跃记录则返回已有的，否则创建新记录</returns>
    public static AlarmRecord GetOrAddRecord(String category, String action, String groupName)
    {
        // 查找该类别和操作下正在报警中的记录
        var list = AlarmRecord.FindAllByCategoryAndActionAndStatus(category, action, AlarmStatuses.Alarming);
        if (list.Count > 0) return list[0];

        var group = AlarmGroup.FindByName(groupName);
        var record = new AlarmRecord
        {
            Name = $"{category}-{action}",
            GroupId = group?.Id ?? 0,
            Category = category,
            Action = action,
            Status = AlarmStatuses.Alarming,
            StartTime = DateTime.Now,
            Times = 0,

            Creator = Environment.MachineName,
        };

        record.Insert();

        return record;
    }

    /// <summary>发送告警并记录报警生命周期</summary>
    /// <param name="groupName">告警组</param>
    /// <param name="webhook">告警Url</param>
    /// <param name="title">告警标题</param>
    /// <param name="message">告警消息</param>
    /// <param name="category">类别。如应用下线、节点下线、CPU告警等</param>
    /// <param name="action">操作。报警对象，如应用名或节点名</param>
    /// <returns>报警记录</returns>
    public static AlarmRecord SendAlarmWithRecord(String groupName, String webhook, String title, String message, String category, String action)
    {
        // 获取或创建报警记录
        var record = GetOrAddRecord(category, action, groupName);

        // 更新报警记录
        record.Times++;
        record.Content = message;
        if (record.StartTime.Year > 2000)
            record.Duration = (Int32)(DateTime.Now - record.StartTime).TotalSeconds;
        record.Update();

        // 发送告警通知
        SendAlarm(groupName, webhook, title, message);

        return record;
    }

    /// <summary>恢复报警。标记报警结束并发送恢复通知</summary>
    /// <param name="category">类别</param>
    /// <param name="action">操作</param>
    /// <param name="groupName">告警组名</param>
    /// <param name="webhook">告警Url</param>
    /// <returns>是否有报警被恢复</returns>
    public static Boolean RecoverAlarm(String category, String action, String groupName, String webhook)
    {
        var list = AlarmRecord.FindAllByCategoryAndActionAndStatus(category, action, AlarmStatuses.Alarming);
        if (list.Count == 0) return false;

        foreach (var record in list)
        {
            record.Status = AlarmStatuses.Recovered;
            record.EndTime = DateTime.Now;
            if (record.StartTime.Year > 2000)
                record.Duration = (Int32)(record.EndTime - record.StartTime).TotalSeconds;
            record.Update();

            // 发送恢复通知
            var duration = TimeSpan.FromSeconds(record.Duration);
            var msg = $"### 【恢复】{record.Category}\n" +
                      $">**对象：**{record.Action}\n" +
                      $">**开始时间：**{record.StartTime:yyyy-MM-dd HH:mm:ss}\n" +
                      $">**恢复时间：**{record.EndTime:yyyy-MM-dd HH:mm:ss}\n" +
                      $">**持续时间：**{duration.Hours}小时{duration.Minutes}分{duration.Seconds}秒\n" +
                      $">**通知次数：**{record.Times}次";

            SendAlarm(groupName, webhook, $"【恢复】{record.Category}", msg);
        }

        return true;
    }
}