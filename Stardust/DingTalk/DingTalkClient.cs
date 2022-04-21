using NewLife;
using NewLife.Log;
using NewLife.Remoting;
using Stardust.WeiXin;

namespace Stardust.DingTalk
{
    /// <summary>钉钉机器人</summary>
    public class DingTalkClient
    {
        #region 属性
        /// <summary>服务地址</summary>
        public String Url { get; set; } = "https://oapi.dingtalk.com/robot/send?access_token={access_token}";

        /// <summary>性能追踪</summary>
        public ITracer Tracer { get; set; } = DefaultTracer.Instance;

        private HttpClient _Client;
        #endregion

        #region 发送消息
        private async Task<Object> PostAsync(Object msg)
        {
            if (_Client == null)
            {
                _Client = Tracer.CreateHttpClient();
            }

            return await _Client.PostAsync<Object>(Url, msg);
        }

        /// <summary>发送文本消息</summary>
        /// <param name="content">消息内容</param>
        /// <param name="mentions">提醒人。手机号，支持all</param>
        public void SendText(String content, String[] mentions)
        {
            if (content.IsNullOrEmpty()) return;

            // 分解手机号
            var mobiles = mentions?.Where(e => e.Length == 11 && e.ToLong() > 0).ToArray();

            WriteLog(content);

            var msg = new
            {
                msgtype = "text",
                text = new
                {
                    content,
                },
                at = new
                {
                    atMobiles = mobiles,
                    isAll = mentions.Contains("all"),
                },
            };

            PostAsync(msg).Wait();
        }

        /// <summary>
        /// 格式化Markdown数据
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        public String FormatMarkdown(String text)
        {
            // 换行
            text = text.Replace(Environment.NewLine, "\n\n");

            text = text.Replace("<font color=\"info\">", "<font color=\"gray\">");
            text = text.Replace("<font color=\"warn\">", "<font color=\"yellow\">");
            text = text.Replace("<font color=\"success\">", "<font color=\"green\">");

            return text;
        }

        /// <summary>发送markdown</summary>
        /// <param name="title"></param>
        /// <param name="text"></param>
        /// <param name="mentions">提醒人。手机号，支持all</param>
        public void SendMarkDown(String title, String text, String[] mentions)
        {
            if (text.IsNullOrEmpty()) return;

            // 超长阶段
            if (text.Length > 2048) text = text.Substring(0, 2048);

            //WriteLog(text);

            // 分解手机号
            var mobiles = mentions?.Where(e => e.Length == 11 && e.ToLong() > 0).ToArray();

            var msg = new
            {
                msgtype = "markdown",
                markdown = new
                {
                    title,
                    text,
                },
                at = new
                {
                    atMobiles = mobiles,
                    isAll = mentions != null && mentions.Contains("all"),
                },
            };

            PostAsync(msg).Wait();
        }

        /// <summary>发送图文，文章列表</summary>
        /// <param name="article"></param>
        public void SendLink(Article article)
        {
            var msg = new
            {
                msgtype = "link",
                link = new
                {
                    text = article.Description,
                    title = article.Title,
                    picUrl = article.PicUrl,
                    messageUrl = article.Url,
                },
            };

            PostAsync(msg).Wait();
        }
        #endregion

        #region 日志
        /// <summary>日志</summary>
        public ILog Log { get; set; }

        /// <summary>写日志</summary>
        /// <param name="format"></param>
        /// <param name="args"></param>
        public void WriteLog(String format, params Object[] args) => Log?.Info(format, args);
        #endregion
    }
}