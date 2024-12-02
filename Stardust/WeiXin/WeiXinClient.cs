using NewLife;
using NewLife.Log;
using NewLife.Remoting;

namespace Stardust.WeiXin;

/// <summary>企业微信机器人。webhook推送</summary>
public class WeiXinClient
{
    #region 属性
    /// <summary>服务地址</summary>
    public String Url { get; set; } = "https://qyapi.weixin.qq.com/cgi-bin/webhook/send?key={key}";

    /// <summary>性能追踪</summary>
    public ITracer? Tracer { get; set; } = DefaultTracer.Instance;

    private HttpClient? _Client;
    #endregion

    #region 发送消息
    private Task<Object?> PostAsync(Object msg)
    {
        _Client ??= Tracer.CreateHttpClient();

        return _Client.PostAsync<Object>(Url, msg);
    }

    /// <summary>发送文本消息</summary>
    /// <param name="content">消息内容</param>
    /// <param name="mentions">提醒人。userid，或者手机号，支持@all</param>
    public void SendText(String content, params String[] mentions)
    {
        if (content.IsNullOrEmpty()) return;

        // 分解手机号
        var mentioned_list = mentions?.Where(e => e.Length != 11 || e.ToLong() == 0).ToArray();
        var mentioned_mobile_list = mentions?.Where(e => e.Length == 11 && e.ToLong() > 0).ToArray();

        //WriteLog(content);

        var msg = new
        {
            msgtype = "text",
            text = new
            {
                content,
                mentioned_list,
                mentioned_mobile_list,
            },
        };

        PostAsync(msg).Wait();
    }

    /// <summary>发送markdown</summary>
    /// <param name="content"></param>
    public void SendMarkDown(String content)
    {
        if (content.IsNullOrEmpty()) return;

        // 超长截断
        if (content.Length > 2048) content = content.Substring(0, 2048);

        WriteLog(content);

        var msg = new
        {
            msgtype = "markdown",
            markdown = new
            {
                content,
            },
        };

        PostAsync(msg).Wait();
    }

    /// <summary>发送图片</summary>
    /// <param name="image"></param>
    public void SendImage(Byte[] image)
    {
        if (image == null) return;

        var base64 = image.ToBase64();
        var md5 = image.MD5().ToHex().ToLower();

        var msg = new
        {
            msgtype = "image",
            text = new
            {
                base64,
                md5,
            },
        };

        PostAsync(msg).Wait();
    }

    /// <summary>发送图文，文章列表</summary>
    /// <param name="articles"></param>
    public void SendNews(params Article[] articles)
    {
        if (articles == null || articles.Length == 0) return;

        var msg = new
        {
            msgtype = "news",
            articles,
        };

        PostAsync(msg).Wait();
    }
    #endregion

    #region 日志
    /// <summary>日志</summary>
    public ILog Log { get; set; } = Logger.Null;

    /// <summary>写日志</summary>
    /// <param name="format"></param>
    /// <param name="args"></param>
    public void WriteLog(String format, params Object[] args) => Log?.Info(format, args);
    #endregion
}