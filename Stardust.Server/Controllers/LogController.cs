using Microsoft.AspNetCore.Mvc;
using NewLife;
using NewLife.Collections;
using NewLife.Remoting.Extensions;
using Stardust.Data;

namespace Stardust.Server.Controllers;

[ApiController]
[Route("[controller]")]
public class LogController : ControllerBase
{
    private readonly StarServerSetting _setting;

    public LogController(StarServerSetting setting) => _setting = setting;

    [HttpGet]
    public Object Get() => "LogController";

    [HttpPost]
    public async Task<EmptyResult> Post()
    {
        var r = new StreamReader(Request.Body);
        var content = await r.ReadToEndAsync();
        if (!content.IsNullOrEmpty())
        {
            var appId = Request.Headers["X-AppId"] + "";
            var clientId = Request.Headers["X-ClientId"] + "";
            var ip = HttpContext.GetUserHost();

            // 验证应用
            var app = App.FindByName(appId);
            if (app == null && !appId.IsNullOrEmpty())
            {
                app = new App
                {
                    Name = appId,
                    Enable = _setting.AppAutoRegister,
                };
                app.Insert();
            }
            if (app != null && app.Enable)
            {
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
                        sb.Clear();
                    }
                }
            }
        }

        return new EmptyResult();
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