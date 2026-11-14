using Microsoft.AspNetCore.Mvc;
using Stardust.Data.Deployment;
using NewLife;
using NewLife.Cube;
using NewLife.Cube.ViewModels;
using NewLife.Web;
using XCode.Membership;

namespace Stardust.Web.Areas.Gateway.Controllers;

/// <summary>网关证书。SSL证书配置，统一使用星尘部署中心的 SslCertificate 管理</summary>
/// <remarks>改继承 WebEntityController（2026-11-14）：接入行级掩码机制与 Insert/Update 双通道补值修复层，
/// 收口此前直继承裸 EntityController 的脱敏/裸奔面（Q5 上游化论据的本地部分）。</remarks>
[Menu(10, true, Icon = "fa-lock")]
[GatewayArea]
public class GatewayCertController : WebEntityController<SslCertificate>
{
    static GatewayCertController()
    {
        ListFields.RemoveCreateField().RemoveUpdateField().RemoveRemarkField();
        ListFields.RemoveField("PfxPassword", "Issuer", "Subject", "NotBefore", "NotAfter", "Thumbprint",
            "AutoRenew", "Provider", "RenewDays", "LastRenew",
            "CreateUserId", "CreateIP", "UpdateUserId", "UpdateIP");

        AddFormFields.RemoveField("Id".Split(","));
        EditFormFields.RemoveField("Id".Split(","));
        // OBS-3 安全裁剪：meta 编辑表单（form 区段）不得下发 PfxPassword 字段（与 Deployment 版对称）
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

        return SslCertificate.Search(null, enable, start, end, p["Q"], p);
    }

    /// <summary>行级掩码规则（声明式，替代分散的 CropPfx 与 Detail/Insert/Update/Delete override，语义逐键保真）：
    /// 全视图（列表/详情/导出/写响应）PfxPassword 值级置 null（键不删，OBS-3）；
    /// 写路径响应语义=裁剪，调用方无回显需求；新提交值与旧存量值都不得回显。
    /// 掩码只改写投影行，不触碰共享缓存中的原实体。</summary>
    private static readonly Dictionary<String, Object> _masks = new(StringComparer.OrdinalIgnoreCase) { ["PfxPassword"] = null };

    /// <summary>全视图统一掩码（原行为：五路出参全掩）。</summary>
    protected override IDictionary<String, Object> GetRowMasks(ViewKinds view) => _masks;

}
