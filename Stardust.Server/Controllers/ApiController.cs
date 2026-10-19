using System.ComponentModel;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using NewLife;
using NewLife.Reflection;
using NewLife.Remoting;
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
    public Object Get(String? state)
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

        // 公网健康探针：保留应用标识(name/title/version)便于域名/反代识别应用，隐藏OS/编译/端口/主机等环境指纹
        if (IsIntranet(HttpContext)) return rs;

        return new { Name = asmx?.Name, asmx?.Title, Version = asmx?.FileVersion, Time = DateTime.Now, State = state };
    }

    /// <summary>获取所有接口信息</summary>
    /// <returns></returns>
    [Route("[controller]/[action]")]
    [HttpGet]
    public IList<String> All()
    {
        // 安全V-13：接口枚举仅内网可见，防止公网侦察接口面
        if (!IsIntranet(HttpContext)) throw new ApiException(ApiCode.Forbidden, L.T("The API list is accessible only from the intranet."));

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

    #region 辅助
    /// <summary>是否内网来源。优先代理头，否则按私网网段判断</summary>
    /// <param name="context">HTTP上下文</param>
    private static Boolean IsIntranet(HttpContext context)
    {
        var ip = GetClientIP(context);
        return ip.IsNullOrEmpty() || IsPrivate(ip);
    }

    /// <summary>获取客户端IP。优先取 X-Forwarded-For / X-Real-IP（反代场景），否则直连地址</summary>
    private static String GetClientIP(HttpContext context)
    {
        var fwd = context.Request.Headers["X-Forwarded-For"].ToString();
        if (!fwd.IsNullOrEmpty())
        {
            var p = fwd.IndexOf(',');
            if (p > 0) fwd = fwd[..p];
            return fwd.Trim();
        }

        var real = context.Request.Headers["X-Real-IP"].ToString();
        if (!real.IsNullOrEmpty()) return real.Trim();

        return context.Connection.RemoteIpAddress + "";
    }

    /// <summary>判断是否为内网/回环地址</summary>
    private static Boolean IsPrivate(String ip)
    {
        if (!IPAddress.TryParse(ip, out var addr)) return false;
        if (IPAddress.IsLoopback(addr)) return true;
        if (addr.AddressFamily == AddressFamily.InterNetworkV6) return addr.IsIPv6LinkLocal || addr.IsIPv6SiteLocal || addr.IsIPv6Multicast;

        var b = addr.GetAddressBytes();
        return b[0] == 10
            || (b[0] == 172 && b[1] >= 16 && b[1] <= 31)
            || (b[0] == 192 && b[1] == 168)
            || (b[0] == 169 && b[1] == 254);
    }
    #endregion
}