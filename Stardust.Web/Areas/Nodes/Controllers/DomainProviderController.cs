using Microsoft.AspNetCore.Mvc;
using Stardust.Data.Nodes;
using NewLife;
using NewLife.Cube;
using NewLife.Cube.Extensions;
using NewLife.Cube.ViewModels;
using NewLife.Log;
using NewLife.Web;
using XCode.Membership;
using static Stardust.Data.Nodes.DomainProvider;

namespace Stardust.Web.Areas.Nodes.Controllers;

/// <summary>域名供应商。域名对应的DNS供应商凭据配置</summary>
[Menu(120, true, Icon = "fa-table")]
[NodesArea]
public class DomainProviderController : EntityController<DomainProvider>
{
    static DomainProviderController()
    {
        ListFields.RemoveCreateField().RemoveRemarkField();

        // 列表页隐藏敏感字段
        ListFields.RemoveField("AppSecret", "DNSZoneId");

        // 列表页AppKey显示掩码
        {
            var df = ListFields.GetField("AppKey") as ListField;
            df.GetValue = e => MaskKey(((DomainProvider)e).AppKey);
        }

        // 搜索字段
        SearchFields.AddField("Provider");
        SearchFields.AddField("Domain");
        SearchFields.AddField("Enable");
        SearchFields.AddField("Endpoint");
        SearchFields.AddField("Region");
    }

    /// <summary>掩码密钥，显示前4位+后4位，中间隐藏</summary>
    /// <param name="key"></param>
    /// <returns></returns>
    private static String MaskKey(String key)
    {
        if (key.IsNullOrEmpty() || key.Length <= 8) return key;

        return key[..4] + "****" + key[^4..];
    }

    //private readonly ITracer _tracer;

    //public DomainProviderController(ITracer tracer)
    //{
    //    _tracer = tracer;
    //}

    /// <summary>高级搜索。列表页查询、导出Excel、导出Json、分享页等使用</summary>
    /// <param name="p">分页器。包含分页排序参数，以及Http请求参数</param>
    /// <returns></returns>
    /// <summary>高级搜索。按条件分页查询</summary>
    /// <param name="p">分页参数</param>
    /// <returns>实体列表</returns>
    protected override IEnumerable<DomainProvider> Search(Pager p)
    {
        var domain = p["domain"];
        var enable = p["enable"]?.ToBoolean();

        var start = p["dtStart"].ToDateTime();
        var end = p["dtEnd"].ToDateTime();

        return DomainProvider.Search(domain, enable, start, end, p["Q"], p);
    }
}
