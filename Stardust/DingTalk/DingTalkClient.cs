using NewLife;
using NewLife.Log;
using NewLife.Remoting;
using Stardust.WeiXin;

namespace Stardust.DingTalk;

/// <summary>钉钉机器人</summary>
public class DingTalkClient
{
    #region 属性
    /// <summary>服务地址</summary>
    public String Url { get; set; } = "https://oapi.dingtalk.com/robot/send?access_token={access_token}";

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
                isAll = mentions != null && mentions.Contains("all"),
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
        text = text.Replace(Environment.NewLine, "  \n  ");
        // 经测试 手机不支持<br/> 后期支持了再删除 这要在放在处理完换行以后
        text = text.Replace("<br/>", "  \n  ");

        // 经测试 手机不支持<font color="red"> 只支持HEX值   
        text = text.Replace("<font color=\"info\">", "<font color=\"#B3B3B3\">");
        text = text.Replace("<font color=\"warn\">", "<font color=\"#FFFF66\">");
        text = text.Replace("<font color=\"success\">", "<font color=\"#66FF66\">");
        ////MarkDown颜色列表 
        text = text.Replace("<font color=\"AliceBlue\">", "<font color=\"#F0F8FF\">");
        text = text.Replace("<font color=\"AntiqueWhite\">", "<font color=\"#FAEBD7\">");
        text = text.Replace("<font color=\"Aqua\">", "<font color=\"#00FFFF\">");
        text = text.Replace("<font color=\"Aquamarine\">", "<font color=\"#7FFFD4\">");
        text = text.Replace("<font color=\"Azure\">", "<font color=\"#F0FFFF\">");
        text = text.Replace("<font color=\"Beige\">", "<font color=\"#F5F5DC\">");
        text = text.Replace("<font color=\"Bisque\">", "<font color=\"#FFE4C4\">");
        text = text.Replace("<font color=\"Black\">", "<font color=\"#000000\">");
        text = text.Replace("<font color=\"BlanchedAlmond\">", "<font color=\"#FFEBCD\">");
        text = text.Replace("<font color=\"Blue\">", "<font color=\"#0000FF\">");
        text = text.Replace("<font color=\"BlueViolet\">", "<font color=\"#8A2BE2\">");
        text = text.Replace("<font color=\"Brown\">", "<font color=\"#A52A2A\">");
        text = text.Replace("<font color=\"BurlyWood\">", "<font color=\"#DEB887\">");
        text = text.Replace("<font color=\"CadetBlue\">", "<font color=\"#5F9EA0\">");
        text = text.Replace("<font color=\"Chartreuse\">", "<font color=\"#7FFF00\">");
        text = text.Replace("<font color=\"Chocolate\">", "<font color=\"#D2691E\">");
        text = text.Replace("<font color=\"Coral\">", "<font color=\"#FF7F50\">");
        text = text.Replace("<font color=\"CornflowerBlue\">", "<font color=\"#6495ED\">");
        text = text.Replace("<font color=\"Cornsilk\">", "<font color=\"#FFF8DC\">");
        text = text.Replace("<font color=\"Crimson\">", "<font color=\"#DC143C\">");
        text = text.Replace("<font color=\"Cyan\">", "<font color=\"#00FFFF\">");
        text = text.Replace("<font color=\"DarkBlue\">", "<font color=\"#00008B\">");
        text = text.Replace("<font color=\"DarkCyan\">", "<font color=\"#008B8B\">");
        text = text.Replace("<font color=\"DarkGoldenRod\">", "<font color=\"#B8860B\">");
        text = text.Replace("<font color=\"DarkGray\">", "<font color=\"#A9A9A9\">");
        text = text.Replace("<font color=\"DarkGreen\">", "<font color=\"#006400\">");
        text = text.Replace("<font color=\"DarkKhaki\">", "<font color=\"#BDB76B\">");
        text = text.Replace("<font color=\"DarkMagenta\">", "<font color=\"#8B008B\">");
        text = text.Replace("<font color=\"DarkOliveGreen\">", "<font color=\"#556B2F\">");
        text = text.Replace("<font color=\"Darkorange\">", "<font color=\"#FF8C00\">");
        text = text.Replace("<font color=\"DarkOrchid\">", "<font color=\"#9932CC\">");
        text = text.Replace("<font color=\"DarkRed\">", "<font color=\"#8B0000\">");
        text = text.Replace("<font color=\"DarkSalmon\">", "<font color=\"#E9967A\">");
        text = text.Replace("<font color=\"DarkSeaGreen\">", "<font color=\"#8FBC8F\">");
        text = text.Replace("<font color=\"DarkSlateBlue\">", "<font color=\"#483D8B\">");
        text = text.Replace("<font color=\"DarkSlateGray\">", "<font color=\"#2F4F4F\">");
        text = text.Replace("<font color=\"DarkTurquoise\">", "<font color=\"#00CED1\">");
        text = text.Replace("<font color=\"DarkViolet\">", "<font color=\"#9400D3\">");
        text = text.Replace("<font color=\"DeepPink\">", "<font color=\"#FF1493\">");
        text = text.Replace("<font color=\"DeepSkyBlue\">", "<font color=\"#00BFFF\">");
        text = text.Replace("<font color=\"DimGray\">", "<font color=\"#696969\">");
        text = text.Replace("<font color=\"DodgerBlue\">", "<font color=\"#1E90FF\">");
        text = text.Replace("<font color=\"Feldspar\">", "<font color=\"#D19275\">");
        text = text.Replace("<font color=\"FireBrick\">", "<font color=\"#B22222\">");
        text = text.Replace("<font color=\"FloralWhite\">", "<font color=\"#FFFAF0\">");
        text = text.Replace("<font color=\"ForestGreen\">", "<font color=\"#228B22\">");
        text = text.Replace("<font color=\"Fuchsia\">", "<font color=\"#FF00FF\">");
        text = text.Replace("<font color=\"Gainsboro\">", "<font color=\"#DCDCDC\">");
        text = text.Replace("<font color=\"GhostWhite\">", "<font color=\"#F8F8FF\">");
        text = text.Replace("<font color=\"Gold\">", "<font color=\"#FFD700\">");
        text = text.Replace("<font color=\"GoldenRod\">", "<font color=\"#DAA520\">");
        text = text.Replace("<font color=\"Gray\">", "<font color=\"#808080\">");
        text = text.Replace("<font color=\"Green\">", "<font color=\"#008000\">");
        text = text.Replace("<font color=\"GreenYellow\">", "<font color=\"#ADFF2F\">");
        text = text.Replace("<font color=\"HoneyDew\">", "<font color=\"#F0FFF0\">");
        text = text.Replace("<font color=\"HotPink\">", "<font color=\"#FF69B4\">");
        text = text.Replace("<font color=\"IndianRed\">", "<font color=\"#CD5C5C\">");
        text = text.Replace("<font color=\"Indigo\">", "<font color=\"#4B0082\">");
        text = text.Replace("<font color=\"Ivory\">", "<font color=\"#FFFFF0\">");
        text = text.Replace("<font color=\"Khaki\">", "<font color=\"#F0E68C\">");
        text = text.Replace("<font color=\"Lavender\">", "<font color=\"#E6E6FA\">");
        text = text.Replace("<font color=\"LavenderBlush\">", "<font color=\"#FFF0F5\">");
        text = text.Replace("<font color=\"LawnGreen\">", "<font color=\"#7CFC00\">");
        text = text.Replace("<font color=\"LemonChiffon\">", "<font color=\"#FFFACD\">");
        text = text.Replace("<font color=\"LightBlue\">", "<font color=\"#ADD8E6\">");
        text = text.Replace("<font color=\"LightCoral\">", "<font color=\"#F08080\">");
        text = text.Replace("<font color=\"LightCyan\">", "<font color=\"#E0FFFF\">");
        text = text.Replace("<font color=\"LightGoldenRodYellow\">", "<font color=\"#FAFAD2\">");
        text = text.Replace("<font color=\"LightGrey\">", "<font color=\"#D3D3D3\">");
        text = text.Replace("<font color=\"LightGreen\">", "<font color=\"#90EE90\">");
        text = text.Replace("<font color=\"LightPink\">", "<font color=\"#FFB6C1\">");
        text = text.Replace("<font color=\"LightSalmon\">", "<font color=\"#FFA07A\">");
        text = text.Replace("<font color=\"LightSeaGreen\">", "<font color=\"#20B2AA\">");
        text = text.Replace("<font color=\"LightSkyBlue\">", "<font color=\"#87CEFA\">");
        text = text.Replace("<font color=\"LightSlateBlue\">", "<font color=\"#8470FF\">");
        text = text.Replace("<font color=\"LightSlateGray\">", "<font color=\"#778899\">");
        text = text.Replace("<font color=\"LightSteelBlue\">", "<font color=\"#B0C4DE\">");
        text = text.Replace("<font color=\"LightYellow\">", "<font color=\"#FFFFE0\">");
        text = text.Replace("<font color=\"Lime\">", "<font color=\"#00FF00\">");
        text = text.Replace("<font color=\"LimeGreen\">", "<font color=\"#32CD32\">");
        text = text.Replace("<font color=\"Linen\">", "<font color=\"#FAF0E6\">");
        text = text.Replace("<font color=\"Magenta\">", "<font color=\"#FF00FF\">");
        text = text.Replace("<font color=\"Maroon\">", "<font color=\"#800000\">");
        text = text.Replace("<font color=\"MediumAquaMarine\">", "<font color=\"#66CDAA\">");
        text = text.Replace("<font color=\"MediumBlue\">", "<font color=\"#0000CD\">");
        text = text.Replace("<font color=\"MediumOrchid\">", "<font color=\"#BA55D3\">");
        text = text.Replace("<font color=\"MediumPurple\">", "<font color=\"#9370D8\">");
        text = text.Replace("<font color=\"MediumSeaGreen\">", "<font color=\"#3CB371\">");
        text = text.Replace("<font color=\"MediumSlateBlue\">", "<font color=\"#7B68EE\">");
        text = text.Replace("<font color=\"MediumSpringGreen\">", "<font color=\"#00FA9A\">");
        text = text.Replace("<font color=\"MediumTurquoise\">", "<font color=\"#48D1CC\">");
        text = text.Replace("<font color=\"MediumVioletRed\">", "<font color=\"#C71585\">");
        text = text.Replace("<font color=\"MidnightBlue\">", "<font color=\"#191970\">");
        text = text.Replace("<font color=\"MintCream\">", "<font color=\"#F5FFFA\">");
        text = text.Replace("<font color=\"MistyRose\">", "<font color=\"#FFE4E1\">");
        text = text.Replace("<font color=\"Moccasin\">", "<font color=\"#FFE4B5\">");
        text = text.Replace("<font color=\"NavajoWhite\">", "<font color=\"#FFDEAD\">");
        text = text.Replace("<font color=\"Navy\">", "<font color=\"#000080\">");
        text = text.Replace("<font color=\"OldLace\">", "<font color=\"#FDF5E6\">");
        text = text.Replace("<font color=\"Olive\">", "<font color=\"#808000\">");
        text = text.Replace("<font color=\"OliveDrab\">", "<font color=\"#6B8E23\">");
        text = text.Replace("<font color=\"Orange\">", "<font color=\"#FFA500\">");
        text = text.Replace("<font color=\"OrangeRed\">", "<font color=\"#FF4500\">");
        text = text.Replace("<font color=\"Orchid\">", "<font color=\"#DA70D6\">");
        text = text.Replace("<font color=\"PaleGoldenRod\">", "<font color=\"#EEE8AA\">");
        text = text.Replace("<font color=\"PaleGreen\">", "<font color=\"#98FB98\">");
        text = text.Replace("<font color=\"PaleTurquoise\">", "<font color=\"#AFEEEE\">");
        text = text.Replace("<font color=\"PaleVioletRed\">", "<font color=\"#D87093\">");
        text = text.Replace("<font color=\"PapayaWhip\">", "<font color=\"#FFEFD5\">");
        text = text.Replace("<font color=\"PeachPuff\">", "<font color=\"#FFDAB9\">");
        text = text.Replace("<font color=\"Peru\">", "<font color=\"#CD853F\">");
        text = text.Replace("<font color=\"Pink\">", "<font color=\"#FFC0CB\">");
        text = text.Replace("<font color=\"Plum\">", "<font color=\"#DDA0DD\">");
        text = text.Replace("<font color=\"PowderBlue\">", "<font color=\"#B0E0E6\">");
        text = text.Replace("<font color=\"Purple\">", "<font color=\"#800080\">");
        text = text.Replace("<font color=\"Red\">", "<font color=\"#FF0000\">");
        text = text.Replace("<font color=\"RosyBrown\">", "<font color=\"#BC8F8F\">");
        text = text.Replace("<font color=\"RoyalBlue\">", "<font color=\"#4169E1\">");
        text = text.Replace("<font color=\"SaddleBrown\">", "<font color=\"#8B4513\">");
        text = text.Replace("<font color=\"Salmon\">", "<font color=\"#FA8072\">");
        text = text.Replace("<font color=\"SandyBrown\">", "<font color=\"#F4A460\">");
        text = text.Replace("<font color=\"SeaGreen\">", "<font color=\"#2E8B57\">");
        text = text.Replace("<font color=\"SeaShell\">", "<font color=\"#FFF5EE\">");
        text = text.Replace("<font color=\"Sienna\">", "<font color=\"#A0522D\">");
        text = text.Replace("<font color=\"Silver\">", "<font color=\"#C0C0C0\">");
        text = text.Replace("<font color=\"SkyBlue\">", "<font color=\"#87CEEB\">");
        text = text.Replace("<font color=\"SlateBlue\">", "<font color=\"#6A5ACD\">");
        text = text.Replace("<font color=\"SlateGray\">", "<font color=\"#708090\">");
        text = text.Replace("<font color=\"Snow\">", "<font color=\"#FFFAFA\">");
        text = text.Replace("<font color=\"SpringGreen\">", "<font color=\"#00FF7F\">");
        text = text.Replace("<font color=\"SteelBlue\">", "<font color=\"#4682B4\">");
        text = text.Replace("<font color=\"Tan\">", "<font color=\"#D2B48C\">");
        text = text.Replace("<font color=\"Teal\">", "<font color=\"#008080\">");
        text = text.Replace("<font color=\"Thistle\">", "<font color=\"#D8BFD8\">");
        text = text.Replace("<font color=\"Tomato\">", "<font color=\"#FF6347\">");
        text = text.Replace("<font color=\"Turquoise\">", "<font color=\"#40E0D0\">");
        text = text.Replace("<font color=\"Violet\">", "<font color=\"#EE82EE\">");
        text = text.Replace("<font color=\"VioletRed\">", "<font color=\"#D02090\">");
        text = text.Replace("<font color=\"Wheat\">", "<font color=\"#F5DEB3\">");
        text = text.Replace("<font color=\"White\">", "<font color=\"#FFFFFF\">");
        text = text.Replace("<font color=\"WhiteSmoke\">", "<font color=\"#F5F5F5\">");
        text = text.Replace("<font color=\"Yellow\">", "<font color=\"#FFFF00\">");
        text = text.Replace("<font color=\"YellowGreen\">", "<font color=\"#9ACD32\">");
        return text;
    }

    /// <summary>发送markdown</summary>
    /// <param name="title"></param>
    /// <param name="text"></param>
    /// <param name="mentions">提醒人。手机号，支持all</param>
    public void SendMarkDown(String title, String text, String[] mentions)
    {
        if (text.IsNullOrEmpty()) return;

        // 超长截断
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
    public ILog Log { get; set; } = Logger.Null;

    /// <summary>写日志</summary>
    /// <param name="format"></param>
    /// <param name="args"></param>
    public void WriteLog(String format, params Object[] args) => Log?.Info(format, args);
    #endregion
}