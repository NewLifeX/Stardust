using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using NewLife.Cube;
using NewLife.Reflection;

namespace Stardust.Web.Controllers;

public class ApiController : ControllerBaseX
{
    private static readonly String _OS = Environment.OSVersion + "";

    /// <summary>获取所有接口</summary>
    /// <returns></returns>
    [HttpGet]
    public Object Get() => Info(null);

    /// <summary>服务器信息，用户健康检测</summary>
    /// <param name="state">状态信息</param>
    /// <returns></returns>
    [HttpGet(nameof(Info))]
    public Object Info(String state)
    {
        //var conn = HttpContext.Connection;
        var asmx = AssemblyX.Entry;
        var asmx2 = AssemblyX.Create(Assembly.GetExecutingAssembly());

        var ip = HttpContext.GetUserHost();

        var rs = new
        {
            Server = asmx?.Name,
            asmx?.Version,
            OS = _OS,
            ApiVersion = asmx2?.Version,

            Remote = ip + "",
            State = state,
            Time = DateTime.Now,
        };

        return rs;
    }
}