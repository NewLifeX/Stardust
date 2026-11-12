using Microsoft.AspNetCore.Mvc;
using Stardust.Data.Deployment;
using NewLife;
using NewLife.Cube;
using NewLife.Cube.ViewModels;
using NewLife.Web;
using XCode.Membership;

namespace Stardust.Web.Areas.Gateway.Controllers;

/// <summary>网关证书。SSL证书配置，统一使用星尘部署中心的 SslCertificate 管理</summary>
[Menu(10, true, Icon = "fa-lock")]
[GatewayArea]
public class GatewayCertController : EntityController<SslCertificate>
{
    static GatewayCertController()
    {
        ListFields.RemoveCreateField().RemoveUpdateField().RemoveRemarkField();
        ListFields.RemoveField("PfxPassword", "Issuer", "Subject", "NotBefore", "NotAfter", "Thumbprint",
            "AutoRenew", "Provider", "RenewDays", "LastRenew",
            "CreateUserId", "CreateIP", "UpdateUserId", "UpdateIP");

        AddFormFields.RemoveField("Id".Split(","));
        EditFormFields.RemoveField("Id".Split(","));
        // OBS-3 安全裁剪：编辑页不得回显 PfxPassword 明文
        EditFormFields.RemoveField("PfxPassword");

        SearchFields.AddField("Domain");
        SearchFields.AddField("Enable");
        SearchFields.AddField("CreateTime");
    }

    /// <summary>高级搜索。按条件分页查询</summary>
    /// <param name="p">分页参数</param>
    /// <returns>实体列表</returns>
    protected override IEnumerable<SslCertificate> Search(Pager p)
    {
        var domain = p["domain"];
        var enable = p["enable"]?.ToBoolean();

        var start = p["dtStart"].ToDateTime();
        var end = p["dtEnd"].ToDateTime();

        return SslCertificate.Search(null, enable, start, end, p["Q"], p).Select(CropPfx);
    }

    /// <summary>查看单行。出参投影裁剪 PfxPassword 明文（OBS-3）</summary>
    /// <param name="id">主键</param>
    /// <returns>裁剪后的单行视图</returns>
    public override ActionResult Detail(String id)
    {
        var rs = base.Detail(id);
        if (rs is ViewResult view && view.Model is SslCertificate entity)
        {
            // 克隆后再裁剪，避免改动单对象缓存中的实体
            var clone = entity.CloneEntity();
            clone.PfxPassword = null;
            view.ViewData.Model = clone;
        }

        return rs;
    }

    /// <summary>出参投影：克隆实体并置空 PfxPassword 明文，杜绝列表/导出/详情泄漏（OBS-3）。
    /// 列表列虽已裁 PfxPassword，但导出 Excel/Json 走全字段，仍需投影；克隆为全新实例，
    /// 不原地改写共享缓存中的原实体；写路径不经过本投影，语义不变。</summary>
    /// <param name="src">原始实体</param>
    /// <returns>不含密码明文的克隆实体</returns>
    private static SslCertificate CropPfx(SslCertificate src)
    {
        var dst = src.CloneEntity();
        dst.PfxPassword = null;
        return dst;
    }
}
