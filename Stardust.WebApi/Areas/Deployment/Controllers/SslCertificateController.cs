using Microsoft.AspNetCore.Mvc;
using Stardust.Data.Deployment;
using NewLife;
using NewLife.Cube;
using NewLife.Cube.Extensions;
using NewLife.Cube.ViewModels;
using NewLife.Log;
using NewLife.Web;
using XCode.Membership;
using static Stardust.Data.Deployment.SslCertificate;

namespace Stardust.Web.Areas.Deployment.Controllers;

/// <summary>SSL证书。HTTPS证书管理，支持多域名、自动续期、多格式导出</summary>
[Menu(10, true, Icon = "fa-table")]
[DeploymentArea]
public class SslCertificateController : EntityController<SslCertificate>
{
    static SslCertificateController()
    {
        //LogOnChange = true;

        //ListFields.RemoveField("Id", "Creator");
        ListFields.RemoveCreateField().RemoveRemarkField();

        // OBS-3 安全裁剪：meta 列表与编辑表单不得下发 PfxPassword 字段
        ListFields.RemoveField("PfxPassword");
        EditFormFields.RemoveField("PfxPassword");

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

    //private readonly ITracer _tracer;

    //public SslCertificateController(ITracer tracer)
    //{
    //    _tracer = tracer;
    //}

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
    /// <returns>裁剪后的单行数据</returns>
    public override ApiResponse<SslCertificate> Detail(String id)
    {
        var res = base.Detail(id);
        if (res?.Data != null) res.Data = CropPfx(res.Data);
        return res;
    }

    /// <summary>添加数据。响应快照裁剪 PfxPassword 明文（OBS-3 扩围：写路径响应语义=裁剪，调用方无回显需求）</summary>
    /// <param name="model">提交的模型</param>
    /// <returns>裁剪后的新增结果</returns>
    public override async Task<ApiResponse<SslCertificate>> Insert(SslCertificate model)
    {
        var res = await base.Insert(model);
        if (res?.Data != null) res.Data = CropPfx(res.Data);
        return res;
    }

    /// <summary>更新数据。响应快照裁剪 PfxPassword 明文（OBS-3 扩围：新提交值与旧存量值都不得回显）</summary>
    /// <param name="id">主键</param>
    /// <param name="model">提交的模型</param>
    /// <returns>裁剪后的更新结果</returns>
    public override async Task<ApiResponse<SslCertificate>> Update(String id, SslCertificate model)
    {
        var res = await base.Update(id, model);
        if (res?.Data != null) res.Data = CropPfx(res.Data);
        return res;
    }

    /// <summary>删除数据。响应快照裁剪 PfxPassword 明文（OBS-3 扩围：存量密码不得随删除回显流向非创建者）</summary>
    /// <param name="id">主键</param>
    /// <returns>裁剪后的删除结果</returns>
    public override ApiResponse<SslCertificate> Delete(String id)
    {
        var res = base.Delete(id);
        if (res?.Data != null) res.Data = CropPfx(res.Data);
        return res;
    }

    /// <summary>出参投影：克隆实体并置空 PfxPassword 明文，杜绝列表/详情/导出泄漏（OBS-3）。
    /// 克隆为全新实例，不原地改写共享缓存中的原实体；查找数据 FindData 不裁剪，写路径语义不变。</summary>
    /// <param name="src">原始实体</param>
    /// <returns>不含密码明文的克隆实体</returns>
    private static SslCertificate CropPfx(SslCertificate src)
    {
        var dst = new SslCertificate();
        foreach (var fi in Stardust.Data.Deployment.SslCertificate.Meta.Fields)
        {
            dst[fi.Name] = src[fi.Name];
        }
        dst.PfxPassword = null;
        return dst;
    }
}
