using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.Net.Http.Headers;
using NewLife;
using NewLife.Cube;
using NewLife.Data;
using NewLife.Reflection;
using NewLife.Serialization;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace ZTO.Scan.Server.Controllers
{
    public class ApiController : ControllerBaseX
    {
        /// <summary>获取所有接口</summary>
        /// <returns></returns>
        [HttpGet]
        public Object Get() => Info(null);

        private static readonly String _OS = Environment.OSVersion + "";
        private static readonly String _MachineName = Environment.MachineName;
        private static readonly String _UserName = Environment.UserName;
        private static readonly String _LocalIP = NetHelper.MyIP() + "";
        /// <summary>服务器信息，用户健康检测</summary>
        /// <param name="state">状态信息</param>
        /// <returns></returns>
        [HttpGet(nameof(Info))]
        public Object Info(String state)
        {
            var conn = HttpContext.Connection;
            var asmx = AssemblyX.Entry;
            var asmx2 = AssemblyX.Create(Assembly.GetExecutingAssembly());

            var ip = HttpContext.GetUserHost();

            var rs = new
            {
                Server = asmx?.Name,
                asmx?.Version,
                OS = _OS,
                MachineName = _MachineName,
                UserName = _UserName,
                ApiVersion = asmx2?.Version,

                LocalIP = _LocalIP,
                Remote = ip + "",
                State = state,
                Time = DateTime.Now,
            };

            // 转字典
            var dic = rs.ToDictionary();

            dic["Port"] = conn.LocalPort;
            //dic["Online"] = nsvr.SessionCount;
            //dic["MaxOnline"] = nsvr.MaxSessionCount;

            // 进程
            dic["Process"] = GetProcess();

            return dic;
        }

        private Object GetProcess()
        {
            var proc = Process.GetCurrentProcess();

            return new
            {
                Environment.ProcessorCount,
                ProcessId = proc.Id,
                Threads = proc.Threads.Count,
                Handles = proc.HandleCount,
                WorkingSet = proc.WorkingSet64,
                PrivateMemory = proc.PrivateMemorySize64,
                GCMemory = GC.GetTotalMemory(false),
                GC0 = GC.GetGeneration(0),
                GC1 = GC.GetGeneration(1),
                GC2 = GC.GetGeneration(2),
            };
        }

        private static Packet _myInfo;
        /// <summary>服务器信息，用户健康检测，二进制压测</summary>
        /// <param name="state">状态信息</param>
        /// <returns></returns>
        [HttpPost(nameof(Info2))]
        public async Task<ObjectResult> Info2()
        {
            if (_myInfo == null)
            {
                // 不包含时间和远程地址
                var rs = new
                {
                    MachineNam = _MachineName,
                    UserName = _UserName,
                    LocalIP = _LocalIP,
                };
                _myInfo = new Packet(rs.ToJson().GetBytes());
            }

            var buf = new Byte[4096];
            var count = await Request.Body.ReadAsync(buf, 0, buf.Length);
            var state = new Packet(buf, 0, count);

            var pk = _myInfo.Slice(0, -1);
            pk.Append(state);

            var res = new ObjectResult(pk.GetStream());
            res.Formatters.Add(new StreamOutputFormatter());
            res.ContentTypes.Add(new MediaTypeHeaderValue("application/octet-stream"));

            return res;
        }
    }
}