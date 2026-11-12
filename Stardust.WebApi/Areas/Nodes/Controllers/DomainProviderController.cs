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
public class DomainProviderController : NodesEntityController<DomainProvider>
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

        return DomainProvider.Search(domain, enable, start, end, p["Q"], p).Select(CropRow);
    }

    /// <summary>查看单行。出参投影裁剪 AppKey/AppSecret 明文（task-95 二期刀5，OBS-3 CropPfx 同款语义：值级置 null）。
    /// MVC 回显已核实：StarWeb 编辑表单取数=meta+实体路径（自家 EntityController），与 API Detail 独立，本裁剪不影响 MVC 回显。</summary>
    /// <param name="id">主键</param>
    /// <returns>裁剪后的单行数据</returns>
    public override ApiResponse<DomainProvider> Detail(String id)
    {
        var res = base.Detail(id);
        if (res?.Data != null) res.Data = CropRow(res.Data);
        return res;
    }

    /// <summary>出参投影：克隆实体并置空 AppKey/AppSecret 明文，杜绝列表行泄漏（task-94 一期脱敏，OBS-3 CropPfx 同款语义：值级置 null）。
    /// 刀5 起同裁 Detail；克隆为全新实例，不原地改写共享缓存中的原实体。</summary>
    /// <param name="src">原始实体</param>
    /// <returns>不含 AppKey/AppSecret 明文的克隆实体</returns>
    private static DomainProvider CropRow(DomainProvider src)
    {
        var dst = new DomainProvider();
        foreach (var fi in DomainProvider.Meta.Fields)
        {
            dst[fi.Name] = src[fi.Name];
        }
        dst.AppKey = null;
        dst.AppSecret = null;
        return dst;
    }
}
