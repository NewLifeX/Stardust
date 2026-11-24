using Microsoft.AspNetCore.Mvc;
using Stardust.Data.Deployment;
using NewLife;
using NewLife.Cube;
using NewLife.Cube.Extensions;
using NewLife.Cube.ViewModels;
using NewLife.Log;
using NewLife.Web;
using Stardust.Web.Services;
using Stardust.Data.Platform;
using XCode.Membership;
using static Stardust.Data.Deployment.SslCertificate;

namespace Stardust.Web.Areas.Deployment.Controllers;

/// <summary>SSL证书。HTTPS证书管理，支持多域名、自动续期、多格式导出</summary>
[Menu(10, true, Icon = "fa-table")]
[DeploymentArea]
public class SslCertificateController : ProjectScopedEntityController<SslCertificate>
{
    static SslCertificateController()
    {
        //LogOnChange = true;

        //ListFields.RemoveField("Id", "Creator");
        ListFields.RemoveCreateField().RemoveRemarkField();

        // OBS-3 安全裁剪：列表页与编辑页不得渲染 PfxPassword 明文
        ListFields.RemoveField("PfxPassword");
        EditFormFields.RemoveField("PfxPassword");

        {
            var df = ListFields.GetField("Nodes") as ListField;
            if (df != null)
            {
                df.Header = "节点";
                df.Url = "/Deployment/SslCertificateNode?certId={Id}";
                df.Target = "_frame";
            }
        }
        {
            var df = ListFields.AddListField("Push", null, "UpdateTime");
            df.Header = "下发";
            df.DisplayName = "下发";
            df.Title = "把证书文件下发到关联节点";
            df.Url = "/Deployment/SslCertificate/Push?id={Id}";
            df.DataAction = "action";
        }
        {
            var df = ListFields.AddListField("AddAllNodes", null, "UpdateTime");
            df.Header = "关联全部节点";
            df.DisplayName = "关联全部";
            df.Title = "把证书关联到全部启用节点";
            df.Url = "/Deployment/SslCertificate/AddAllNodes?id={Id}";
            df.DataAction = "action";
        }

        //{
        //    var df = ListFields.GetField("Code") as ListField;
        //    df.Url = "?code={Code}";
        //    df.Target = "_blank";
        //}
        //{
        //    var df = ListFields.AddListField("devices", null, "Onlines");
        //    df.DisplayName = "查看设备";
        //    df.Url = "Device?groupId={Id}";
        //    df.DataVisible = e => (e as SslCertificate).Devices > 0;
        //    df.Target = "_frame";
        //}
        //{
        //    var df = ListFields.GetField("Kind") as ListField;
        //    df.GetValue = e => ((Int32)(e as SslCertificate).Kind).ToString("X4");
        //}
        //ListFields.TraceUrl("TraceId");
    }

    private readonly CertService _certService;

    /// <summary>实例化</summary>
    /// <param name="certService">证书下发服务</param>
    public SslCertificateController(CertService certService) => _certService = certService;

    /// <summary>下发证书到关联节点</summary>
    /// <param name="id">证书编号</param>
    /// <returns></returns>
    [EntityAuthorize(PermissionFlags.Update)]
    public async Task<ActionResult> Push(Int32 id)
    {
        var cert = ProjectScopeRules.FilterVisible(SslCertificate.FindById(id));
        if (cert == null) return Json(500, L.T("Certificate not found."));

        var links = ProjectScopeRules.FilterVisible(SslCertificateNode.FindAllByCertId(id)).Where(e => e.Enable).ToList();
        if (links.Count == 0) return JsonRefresh(L.T("This certificate is not associated with any node."), 0);

        var rs = await _certService.PushAsync(cert, links, HttpContext.RequestAborted);

        return JsonRefresh(L.T("Push completed: {0} succeeded, {1} failed, {2} timed out.", rs.Success, rs.Failed, rs.Timeout));
    }

    /// <summary>把证书关联到全部启用节点</summary>
    /// <param name="id">证书编号</param>
    /// <returns></returns>
    [EntityAuthorize(PermissionFlags.Update)]
    public ActionResult AddAllNodes(Int32 id)
    {
        var cert = ProjectScopeRules.FilterVisible(SslCertificate.FindById(id));
        if (cert == null) return Json(500, L.T("Certificate not found."));

        var count = _certService.AddAllNodes(cert);

        return JsonRefresh(L.T("Associated {0} node(s).", count));
    }

    /// <summary>高级搜索。列表页查询、导出Excel、导出Json、分享页等使用</summary>
    /// <param name="p">分页器。包含分页排序参数，以及Http请求参数</param>
    /// <returns></returns>
    /// <summary>高级搜索。按条件分页查询</summary>
    /// <param name="p">分页参数</param>
    /// <returns>实体列表</returns>
    protected override IEnumerable<SslCertificate> Search(Pager p)
    {
        var autoRenew = p["autoRenew"]?.ToBoolean();
        var enable = p["enable"]?.ToBoolean();

        var start = p["dtStart"].ToDateTime();
        var end = p["dtEnd"].ToDateTime();

        return SslCertificate.Search(autoRenew, enable, start, end, p["Q"], p).Select(CropPfx);
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
    /// 克隆为全新实例，不原地改写共享缓存中的原实体；写路径不经过本投影，语义不变。</summary>
    /// <param name="src">原始实体</param>
    /// <returns>不含密码明文的克隆实体</returns>
    private static SslCertificate CropPfx(SslCertificate src)
    {
        var dst = src.CloneEntity();
        dst.PfxPassword = null;
        return dst;
    }
}
