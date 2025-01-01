using System.ComponentModel;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using NewLife;
using NewLife.Reflection;
using NewLife.Remoting.Extensions;

namespace Stardust.Server.Controllers;

[ApiFilter]
[ApiController]
public class ApiController : ControllerBase
{
    private static readonly String _OS = Environment.OSVersion + "";
    private readonly IList<EndpointDataSource> _sources;

    /// <summary>构造函数</summary>
    /// <param name="sources"></param>
    public ApiController(IEnumerable<EndpointDataSource> sources) => _sources = sources.ToList();

    /// <summary>服务器信息，用户健康检测</summary>
    /// <param name="state">状态信息</param>
    /// <returns></returns>
    [Route("[controller]")]
    [HttpGet]
    public Object Get(String state)
    {
        var asmx = AssemblyX.Entry;
        var conn = HttpContext.Connection;
        var remote = conn.RemoteIpAddress;
        if (remote != null && remote.IsIPv4MappedToIPv6) remote = remote.MapToIPv4();
        var ip = HttpContext.GetUserHost();

        var rs = new
        {
            asmx?.Name,
            asmx?.Title,
            asmx?.FileVersion,
            asmx?.Compile,
            OS = _OS,

            UserHost = ip?.ToString(),
            Remote = remote?.ToString(),
            Port = conn.LocalPort,
            Time = DateTime.Now,
            State = state,
        };

        return rs;
    }

    /// <summary>获取所有接口信息</summary>
    /// <returns></returns>
    [Route("[controller]/[action]")]
    [HttpGet]
    public IList<String> All()
    {
        var set = new List<EndpointDataSource>();
        var eps = new List<String>();
        foreach (var item in _sources)
        {
            if (!set.Contains(item))
            {
                set.Add(item);

                //eps.AddRange(item.Endpoints);
                foreach (var elm in item.Endpoints)
                {
                    var area = elm.Metadata.GetMetadata<AreaAttribute>();
                    var disp = elm.Metadata.GetMetadata<DisplayNameAttribute>();
                    var desc = elm.Metadata.GetMetadata<ControllerActionDescriptor>();
                    var post = elm.Metadata.GetMetadata<HttpPostAttribute>();
                    if (desc == null) continue;

                    //var name = area == null ?
                    //    $"{desc.ControllerName}/{desc.ActionName}" :
                    //    $"{area?.RouteValue}/{desc.ControllerName}/{desc.ActionName}";

                    var sb = new StringBuilder();
                    sb.Append(post != null ? "POST " : "GET ");
                    sb.Append(desc.ControllerName);
                    sb.Append('/');
                    sb.Append(desc.ActionName);
                    sb.Append('(');
                    sb.Append(desc.MethodInfo.GetParameters().Join(",", pi => $"{pi.ParameterType.Name} {pi.Name}"));
                    sb.Append(')');

                    var name = sb.ToString();

                    if (!eps.Contains(name)) eps.Add(name);
                }
            }
        }

        return eps;
    }
}