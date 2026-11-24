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
/// <remarks>改继承 WebEntityController（2026-11-14）：接入行级掩码机制与 Insert/Update 双通道补值修复层，
/// 收口此前直继承裸 EntityController 的脱敏/裸奔面（Q5 上游化论据的本地部分）。</remarks>
[Menu(10, true, Icon = "fa-table")]
[DeploymentArea]
public class SslCertificateController : WebEntityController<SslCertificate>
{
    static SslCertificateController()
    {
        //LogOnChange = true;

        //ListFields.RemoveField("Id", "Creator");
        ListFields.RemoveCreateField().RemoveRemarkField();

        // OBS-3 安全裁剪：meta 列表与编辑表单不得下发 PfxPassword 字段
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

        return SslCertificate.Search(autoRenew, enable, start, end, p["Q"], p);
    }

    /// <summary>行级掩码规则（声明式，替代分散的 CropPfx 与 Detail/Insert/Update/Delete override，语义逐键保真）：
    /// 全视图（列表/详情/导出/写响应）PfxPassword 值级置 null（键不删，OBS-3）；
    /// 写路径响应语义=裁剪，调用方无回显需求；新提交值与旧存量值都不得回显。
    /// 查找数据 FindData 不裁剪（原行为）；掩码只改写投影行，不触碰共享缓存中的原实体。</summary>
    private static readonly Dictionary<String, Object> _masks = new(StringComparer.OrdinalIgnoreCase) { ["PfxPassword"] = null };

    /// <summary>全视图统一掩码（原行为：五路出参全掩）。</summary>
    protected override IDictionary<String, Object> GetRowMasks(ViewKinds view) => _masks;

}
