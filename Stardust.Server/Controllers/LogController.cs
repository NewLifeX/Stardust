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
                    while (true)
                    {
                        var line = reader.ReadLine();
                        if (line == null) break;

                        // 跳过线程、类型、名称
                        var p = line.IndexOf(' ');
                        var p2 = p;
                        for (var i = 0; i < 3 && p2 > 0; i++)
                        {
                            p2 = line.IndexOf(' ', p2 + 1);
                        }
                        if (p2 < 0) continue;

                        // 发现新行，保存
                        if (sb.Length > 0 && p == 12 && p2 > 0)
                        {
                            AppLog.Create(app.ID, clientId, sb.ToString(), ip);
                            sb.Clear();
                        }

                        if (sb.Length > 0) sb.AppendLine();
                        sb.AppendLine(line.Substring(p2 + 1));
                    }

                    // 残留
                    if (sb.Length > 0)
                    {
                        AppLog.Create(app.ID, clientId, sb.ToString(), ip);
                        sb.Clear();
                    }
                }
            }

            return new EmptyResult();
        }
    }
}