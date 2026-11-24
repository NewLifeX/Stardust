using Microsoft.AspNetCore.Mvc;
using NewLife;
using NewLife.Collections;
using NewLife.Remoting.Extensions;
using Stardust.Data;

namespace Stardust.Server.Controllers;

/// <summary>应用日志接收控制器。接收客户端上报的标准日志行并写入应用客户端日志表</summary>
[ApiController]
[Route("[controller]")]
public class LogController() : ControllerBase
{
    /// <summary>健康检查</summary>
    /// <returns>控制器标识</returns>
    [HttpGet]
    public Object Get() => "LogController";

    /// <summary>接收应用上报日志。解析标准日志行格式并写入应用客户端日志</summary>
    /// <returns>已识别并入库的日志行数。非 JSON 客户端可忽略</returns>
    [HttpPost]
    public async Task<ActionResult> Post()
    {
        var parsed = 0;

        var r = new StreamReader(Request.Body);
        var content = await r.ReadToEndAsync();
        if (!content.IsNullOrEmpty())
        {
            var appId = Request.Headers["X-AppId"] + "";
            var clientId = Request.Headers["X-ClientId"] + "";
            var ip = HttpContext.GetUserHost();

            // 验证应用。安全V-9：不再按X-AppId自动创建应用，未知来源直接忽略，防止匿名灌数据与自动建表注入
            var app = App.FindByName(appId);
            if (app != null && app.Enable)
            {
                // 携带令牌时校验归属，避免任意来源冒充应用灌日志；旧客户端不带令牌时保持原有行为
                var token = Request.Headers["X-Token"] + "";
                if (!token.IsNullOrEmpty())
                {
                    var online = AppOnline.FindByToken(token);
                    if (online == null || online.AppId != app.Id) return StatusCode(403);
                }

                // 00:00:04.205  7 Y 1 NewLife.Core v8.10.2020.1020
                var lines = content.Split(Environment.NewLine);
                for (var i = 0; i < lines.Length; i++)
                {
                    // 时间、线程、类型、名称
                    var ss = new String[4];
                    var p = ReadExpect(lines[i], ' ', ss);
                    if (p > 0 && ss[0] != null && ss[0].Length == 12 && ss[1].ToInt() > 0)
                    {
                        var msg = lines[i][p..]?.Trim();
                        var sb = Pool.StringBuilder.Get();
                        sb.AppendLine(msg);

                        // 尝试后续行
                        for (var j = i + 1; j < lines.Length; j++)
                        {
                            var ss2 = new String[4];
                            var p2 = ReadExpect(lines[j], ' ', ss2);
                            if (p2 > 0 && ss2[0] != null && ss2[0].Length == 12 && ss2[1].ToInt() > 0) break;

                            sb.AppendLine(lines[j]);
                            i++;
                        }
                        msg = sb.Return(true);

                        AppClientLog.Create(app.Id, clientId, ss, msg, ip);
                        parsed++;
                        sb.Clear();
                    }
                }
            }
        }

        return Ok(new { parsed });
    }

    private static Int32 ReadExpect(String value, Char ch, String[] ss)
    {
        var p = 0;
        for (var i = 0; i < ss.Length && p < value.Length; i++)
        {
            var p2 = value.IndexOf(ch, p);
            if (p2 < 0) break;

            ss[i] = value[p..p2];

            p = p2 + 1;

            // 跳过连续符号
            while (p < value.Length && value[p] == ch) p++;
        }

        return p;
    }
}