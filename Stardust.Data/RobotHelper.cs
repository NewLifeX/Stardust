using System;
using NewLife;
using NewLife.Log;
using Stardust.Data.Monitors;
using Stardust.Data.Nodes;
using Stardust.DingTalk;
using Stardust.WeiXin;

namespace Stardust.Data
{
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

        /// <summary>发送告警</summary>
        /// <param name="groupName">告警组</param>
        /// <param name="webhook">告警Url</param>
        /// <param name="title">告警标题</param>
        /// <param name="message">告警消息</param>
        public static void SendAlarm(String groupName, String webhook, String title, String message)
        {
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
                if (webhook.IsNullOrEmpty()) throw new InvalidOperationException("未设置或未启用告警地址！");

                if (webhook.Contains("qyapi.weixin"))
                {
                    hi.Category = "QyWeixin";

                    var weixin = new WeiXinClient { Url = webhook };

                    using var span = DefaultTracer.Instance?.NewSpan("SendWeixin", message);
                    weixin.SendMarkDown(message);
                }
                else if (webhook.Contains("dingtalk"))
                {
                    hi.Category = "DingTalk";

                    var dingTalk = new DingTalkClient { Url = webhook };

                    using var span = DefaultTracer.Instance?.NewSpan("SendDingTalk", message);
                    dingTalk.SendMarkDown(title, message, null);
                }
            }
            catch (Exception ex)
            {
                hi.Success = false;
                hi.Content += Environment.NewLine + ex.ToString();
            }

            hi.SaveAsync();
        }
    }
}