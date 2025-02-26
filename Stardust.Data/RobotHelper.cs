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
}