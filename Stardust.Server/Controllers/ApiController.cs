using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using NewLife.Reflection;
using Stardust.Server.Common;

namespace Stardust.Server.Controllers;

[ApiController]
[Route("[controller]")]
public class ApiController : ControllerBase
{
    private static readonly String _OS = Environment.OSVersion + "";

    /// <summary>获取所有接口</summary>
    /// <returns></returns>
    [ApiFilter]
    [HttpGet]
    public Object Get() => Info(null);

    /// <summary>服务器信息，用户健康检测</summary>
    /// <param name="state">状态信息</param>
    /// <returns></returns>
    [ApiFilter]
    [HttpGet(nameof(Info))]
    public Object Info(String state)
    {
        var asmx = AssemblyX.Entry;
        var asmx2 = AssemblyX.Create(Assembly.GetExecutingAssembly());

        var ip = HttpContext.GetUserHost();

        var rs = new
        {
            asmx?.Name,
            asmx?.Title,
            asmx?.FileVersion,
            asmx?.Compile,
            OS = _OS,
            ApiVersion = asmx2?.Version,

            UserHost = HttpContext.GetUserHost(),
            Remote = ip + "",
            State = state,
            Time = DateTime.Now,
        };

        return rs;
    }
}