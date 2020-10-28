using System;
using System.IO;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using NewLife;
using Stardust.Data;

namespace Stardust.Server.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class LogController : ControllerBase
    {
        [HttpGet]
        public Object Get() => "LogController";

        [HttpPost]
        public EmptyResult Post()
        {
            //var buffer = Request.BodyReader.ReadAsync().Result.Buffer;
            //var content = Encoding.UTF8.GetString(buffer.FirstSpan);
            //var r = new StreamReader(Request.Body);
            //var content = r.ReadToEnd();
            var content = Request.Body.ToStr();
            if (!content.IsNullOrEmpty())
            {
                var appId = Request.Headers["X-AppId"] + "";
                var clientId = Request.Headers["X-ClientId"] + "";
                var ip = HttpContext.Connection?.RemoteIpAddress.MapToIPv4() + "";
                var set = Setting.Current;

                // 验证应用
                var app = App.FindByName(appId);
                if (app == null && !appId.IsNullOrEmpty())
                {
                    app = new App
                    {
                        Name = appId,
                        Enable = set.AutoRegister,
                    };
                    app.Insert();
                }
                if (app != null && app.Enable)
                {
                    // 00:00:04.205  7 Y 1 NewLife.Core v8.10.2020.1020
                    using var reader = new StringReader(content);
                    var sb = new StringBuilder();
                    var ss = new String[4];
                    while (true)
                    {
                        var line = reader.ReadLine();
                        if (line == null) break;

                        // 时间、线程、类型、名称
                        var ss2 = new String[4];
                        var p = ReadExpect(line, ' ', ss2);
                        if (p < 0) continue;

                        // 发现新行，保存
                        if (sb.Length > 0 && ss2[0] != null && ss2[0].Length == 12 && ss2[1].ToInt() > 0)
                        {
                            AppLog.Create(app.ID, clientId, ss, sb.ToString(), ip);
                            sb.Clear();

                            ss = ss2;
                        }

                        if (sb.Length > 0) sb.AppendLine();
                        sb.AppendLine(line.Substring(p)?.Trim());
                    }

                    // 残留
                    if (sb.Length > 0)
                    {
                        AppLog.Create(app.ID, clientId, ss, sb.ToString(), ip);
                        sb.Clear();
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

                ss[i] = value.Substring(p, p2 - p);

                p = p2 + 1;

                // 跳过连续符号
                while (p < value.Length && value[p] == ch) p++;
            }

            return p;
        }
    }
}