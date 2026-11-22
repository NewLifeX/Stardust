using Microsoft.AspNetCore.Mvc;
using Stardust.Data.Deployment;
using NewLife;
using NewLife.Cube;
using NewLife.Cube.Extensions;
using NewLife.Cube.ViewModels;
using NewLife.Log;
using NewLife.Web;
using Stardust.Web.Services;
using XCode.Membership;
using static Stardust.Data.Deployment.SslCertificateNode;

namespace Stardust.Web.Areas.Deployment.Controllers;

/// <summary>证书节点。SSL证书与节点服务器的关联，控制证书下发到哪些节点</summary>
[Menu(88, false)]
[DeploymentArea]
public class SslCertificateNodeController : DeploymentEntityController<SslCertificateNode>
{
    static SslCertificateNodeController()
    {
        //LogOnChange = true;

        //ListFields.RemoveField("Id", "Creator");
        ListFields.RemoveCreateField().RemoveRemarkField();
        AddFormFields.RemoveCreateField();
        EditFormFields.RemoveCreateField();

        {
            var df = ListFields.GetField("CertId") as ListField;
            if (df != null) df.Url = "/Deployment/SslCertificate/Detail/{CertId}";
        }
        {
            var df = ListFields.AddListField("Push", null, "UpdateTime");
            df.Header = "下发";
            df.DisplayName = "下发";
            df.Title = "把该证书下发到当前节点";
            df.Url = "/Deployment/SslCertificateNode/Push?id={Id}";
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
        //    df.DataVisible = e => (e as SslCertificateNode).Devices > 0;
        //    df.Target = "_frame";
        //}
        //{
        //    var df = ListFields.GetField("Kind") as ListField;
        //    df.GetValue = e => ((Int32)(e as SslCertificateNode).Kind).ToString("X4");
        //}
        //ListFields.TraceUrl("TraceId");
    }

    private readonly CertService _certService;

    /// <summary>实例化</summary>
    /// <param name="certService">证书下发服务</param>
    public SslCertificateNodeController(CertService certService) => _certService = certService;

    /// <summary>下发证书到当前节点</summary>
    /// <param name="id">证书节点编号</param>
    /// <returns></returns>
    [EntityAuthorize(PermissionFlags.Update)]
    public async Task<ActionResult> Push(Int32 id)
    {
        var link = SslCertificateNode.FindById(id);
        if (link?.Cert == null) return Json(500, L.T("Certificate node association not found."));

        var rs = await _certService.PushAsync(link.Cert, [link], HttpContext.RequestAborted);

        return JsonRefresh(L.T("Push completed: {0} succeeded, {1} failed, {2} timed out.", rs.Success, rs.Failed, rs.Timeout));
    }

    /// <summary>批量下发证书到选中节点</summary>
    /// <returns></returns>
    [EntityAuthorize(PermissionFlags.Update)]
    public async Task<ActionResult> BatchPush()
    {
        var links = SelectKeys.Select(e => SslCertificateNode.FindById(e.ToInt())).Where(e => e?.Cert != null).ToList();
        if (links.Count == 0) return JsonRefresh(L.T("No certificate node association is selected."), 0);

        var success = 0;
        var failed = 0;
        var timeout = 0;
        foreach (var group in links.GroupBy(e => e.CertId))
        {
            var cert = group.First().Cert;
            var rs = await _certService.PushAsync(cert, group.ToList(), HttpContext.RequestAborted);
            success += rs.Success;
            failed += rs.Failed;
            timeout += rs.Timeout;
        }

        return JsonRefresh(L.T("Push completed: {0} succeeded, {1} failed, {2} timed out.", success, failed, timeout));
    }

    /// <summary>高级搜索。列表页查询、导出Excel、导出Json、分享页等使用</summary>
    /// <param name="p">分页器。包含分页排序参数，以及Http请求参数</param>
    /// <returns></returns>
    protected override IEnumerable<SslCertificateNode> Search(Pager p)
    {
        var certId = p["certId"].ToInt(-1);
        var nodeId = p["nodeId"].ToInt(-1);
        var enable = p["enable"]?.ToBoolean();

        var start = p["dtStart"].ToDateTime();
        var end = p["dtEnd"].ToDateTime();

        return SslCertificateNode.Search(certId, nodeId, enable, start, end, p["Q"], p);
    }
}