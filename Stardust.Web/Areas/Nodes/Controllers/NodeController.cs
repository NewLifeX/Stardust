using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using NewLife;
using NewLife.Cube;
using NewLife.Cube.ViewModels;
using NewLife.Data;
using NewLife.Web;
using Stardust.Data;
using Stardust.Data.Nodes;
using Stardust.Models;
using Stardust.Dns;
using Stardust.Server.Services;
using Stardust.Services;
using Stardust.Data.Platform;
using XCode;
using XCode.Membership;
using XCode.Model;
using static Stardust.Data.Nodes.Node;

namespace Stardust.Web.Areas.Nodes.Controllers;

/// <summary>节点管理。所有被监控的节点</summary>
[Menu(90)]
[NodesArea]
public class NodeController : NodesEntityController<Node>
{
    private readonly StarFactory _starFactory;
    private readonly DnsService _dnsService;
    private readonly NodeWakeService _wakeService;

    static NodeController()
    {
        //ListFields.RemoveField("Secret", "ProductCode", "CompileTime", "OSVersion", "Architecture", "Dpi", "Resolution", "Processor", "CpuID", "Uuid", "MachineGuid", "DiskID", "MACS", "InstallPath", "Runtime", "Framework", "ProvinceID");

        var list = ListFields;
        list.Clear();
        var allows = new[] { "ID", "ProjectName", "Name", "Code", "Category", "ProductCode", "CityName", "Enable", "Version", "OSKind", "Runtime", "Framework", "IP", "Cpu", "Memory", "LastActive", "OnlineTime", "UpdateTime" };
        foreach (var item in allows)
        {
            list.AddListField(item);
        }

        {
            var df = ListFields.GetField("Name") as ListField;
            df.Url = "/Nodes/Node/Detail?id={ID}";
            df.Target = "_blank";
        }
        {
            var df = ListFields.GetField("ProjectName") as ListField;
            df.Url = "/Platform/GalaxyProject?projectId={ProjectId}";
            df.Target = "_frame";
        }
        //{
        //    var df = ListFields.AddListField("App", "Version");
        //    df.DisplayName = "应用实例";
        //    df.Url = "/Registry/AppOnline?nodeId={ID}";
        //}
        //{
        //    var df = ListFields.AddListField("DeployNodes", "Version");
        //    df.DisplayName = "部署实例";
        //    df.Url = "/Deployment/AppDeployNode?nodeId={ID}";
        //}
        //{
        //    var df = ListFields.AddListField("Meter", "Version");
        //    df.DisplayName = "性能";
        //    df.Url = "/Nodes/NodeData?nodeId={ID}";
        //}
        //{
        //    var df = ListFields.AddListField("History", "Version");
        //    df.DisplayName = "历史";
        //    df.Url = "/Nodes/NodeHistory?nodeId={ID}";
        //}
        //{
        //    var df = ListFields.AddListField("Commands", "Version");
        //    df.DisplayName = "命令";
        //    df.Url = "/Nodes/NodeCommand?nodeId={ID}";
        //}
        {
            var df = ListFields.AddListField("Log", "UpdateTime");
            df.DisplayName = "历史";
            df.Url = "/Nodes/NodeHistory?nodeId={ID}";
            df.Target = "_frame";
        }

        // Domains 已通过 [Category("参数设置")] 自动渲染，无需重复 AddField
    }

    public NodeController(StarFactory starFactory, DnsService dnsService, NodeWakeService wakeService)
    {
        LogOnChange = true;

        _starFactory = starFactory;
        _dnsService = dnsService;
        _wakeService = wakeService;
    }

    public override void OnActionExecuting(ActionExecutingContext filterContext)
    {
        base.OnActionExecuting(filterContext);

        var nodeId = GetRequest("Id").ToInt(-1);
        if (nodeId > 0)
        {
            PageSetting.NavView = "_Node_Nav";
            PageSetting.EnableNavbar = false;
        }
    }

    /// <summary>更新时检测域名变更并记录历史</summary>
    /// <param name="entity"></param>
    /// <returns></returns>
    protected override Int32 OnUpdate(Node entity)
    {
        // 检测域名变更
        if ((entity as IEntity).Dirtys["Domains"])
        {
            var old = ProjectScopeRules.FilterVisible(Node.FindByID(entity.ID));
            if (old != null && old.Domains != entity.Domains)
            {
                entity.WriteHistory(HistoryAction.DomainChanged, true, L.TLog("Domains changed: {0} -> {1}", old.Domains, entity.Domains), ManageProvider.UserHost);
            }
        }

        return base.OnUpdate(entity);
    }

    /// <summary>高级搜索。按条件分页查询</summary>
    /// <param name="p">分页参数</param>
    /// <returns>实体列表</returns>
    protected override IEnumerable<Node> Search(Pager p)
    {
        var nodeId = p["Id"].ToInt(-1);
        if (nodeId > 0)
        {
            var node = ProjectScopeRules.FilterVisible(Node.FindByKey(nodeId));
            if (node != null) return new[] { node };
        }

        var rids = p["areaId"].SplitAsInt("/");
        var provinceId = rids.Length > 0 ? rids[0] : -1;
        var cityId = rids.Length > 1 ? rids[1] : -1;

        var projectId = p["projectId"].ToInt(-1);
        var category = p["category"];
        var product = p["product"];
        var osKind = p["osKind"];
        var version = p["version"];
        var runtime = p["runtime"];
        var framework = p["framework"];
        var arch = p["arch"];
        var enable = p["enable"]?.ToBoolean();

        var start = p["dtStart"].ToDateTime();
        var end = p["dtEnd"].ToDateTime();

        var kind = (OSKinds)osKind.ToInt(-1);
        if (kind < 0)
        {
            if (!Enum.TryParse(osKind, out kind)) kind = (OSKinds)(-1);
        }

        return Node.Search(projectId, provinceId, cityId, category, product, kind, version, runtime, framework, arch, enable, start, end, p["Q"], p);
    }

    /// <summary>搜索</summary>
    /// <param name="category"></param>
    /// <param name="key"></param>
    /// <returns></returns>
    public ActionResult NodeSearch(Int32 projectId, String category, String product, String key = null)
    {
        var page = new PageParameter { PageSize = 20 };

        // 默认排序。一个设备可能多次注册节点，导致重复，这里按最后登录时间降序
        if (page.Sort.IsNullOrEmpty())
        {
            page.Sort = _.LastActive;
            page.Desc = true;
        }

        // 优先本项目节点，再全局节点
        var list = Node.Search(projectId, true, category, product, true, key, page);
        list = list.OrderByDescending(e => e.ProjectId == projectId).ThenByDescending(e => e.LastActive).ToList();

        return Json(0, null, list.Select(e => new
        {
            e.ID,
            e.Code,
            e.Name,
            e.ProjectName,
            e.Category,
            e.IP,
        }).ToArray());
    }

    protected override String OnJsonSerialize(Object data)
    {
        var rs = base.OnJsonSerialize(data);

        return rs;
    }

    public async Task<ActionResult> Trace(Int32 id)
    {
        var node = ProjectScopeRules.FilterVisible(FindByID(id));
        if (node != null)
        {
            //NodeCommand.Add(node, "截屏");
            //NodeCommand.Add(node, "抓日志");

            await _starFactory.SendNodeCommandAsync(node.Code, "截屏", cancellationToken: HttpContext.RequestAborted);
            await _starFactory.SendNodeCommandAsync(node.Code, "抓日志", cancellationToken: HttpContext.RequestAborted);
        }

        return RedirectToAction("Index");
    }

    /// <summary>唤醒。由同网段在线节点代发 Wake-on-LAN 魔术包</summary>
    /// <param name="id">节点编号</param>
    /// <returns></returns>
    [EntityAuthorize(PermissionFlags.Update)]
    public async Task<ActionResult> WakeUp(Int32 id)
    {
        var node = ProjectScopeRules.FilterVisible(FindByID(id));
        if (node == null) return Json(400, L.T("Node does not exist."));
        if (node.MACs.IsNullOrEmpty()) return Json(400, L.T("The node has no MAC address and cannot be woken."));

        var count = await _wakeService.WakeAsync(node, HttpContext.RequestAborted);
        if (count == 0) return Json(400, L.T("No online node in the same LAN; unable to wake the node."));

        return JsonRefresh(L.T("Wake-on-LAN command sent to {0} node(s).", count));
    }

    /// <summary>批量唤醒。对选中节点逐个挑选同网段在线节点代发 Wake-on-LAN 魔术包</summary>
    /// <returns></returns>
    [EntityAuthorize(PermissionFlags.Update)]
    public async Task<ActionResult> WakeUpBatch()
    {
        var count = 0;
        foreach (var item in SelectKeys)
        {
            var node = ProjectScopeRules.FilterVisible(FindByID(item.ToInt()));
            if (node == null || node.MACs.IsNullOrEmpty()) continue;

            if (await _wakeService.WakeAsync(node, HttpContext.RequestAborted) > 0) count++;
        }

        if (count == 0) return Json(400, L.T("No online node in the same LAN; unable to wake the node."));

        return JsonRefresh(L.T("Wake-on-LAN command sent to {0} node(s).", count));
    }

    [EntityAuthorize(PermissionFlags.Update)]
    public ActionResult SetAlarm(Boolean enable = true)
    {
        foreach (var item in SelectKeys)
        {
            var dt = ProjectScopeRules.FilterVisible(FindByID(item.ToInt()));
            if (dt != null)
            {
                dt.AlarmOnOffline = enable;
                dt.Save();
            }
        }

        return JsonRefresh("操作成功！");
    }

    [EntityAuthorize(PermissionFlags.Update)]
    public ActionResult ResetAlarm(Int32 alarmRate = 0)
    {
        foreach (var item in SelectKeys)
        {
            var dt = ProjectScopeRules.FilterVisible(FindByID(item.ToInt()));
            if (dt != null)
            {
                dt.AlarmCpuRate = alarmRate;
                dt.AlarmMemoryRate = alarmRate;
                dt.AlarmDiskRate = alarmRate;
                dt.Save();
            }
        }

        return JsonRefresh("操作成功！");
    }

    [EntityAuthorize(PermissionFlags.Update)]
    public ActionResult Fix()
    {
        var bf = new BatchFinder<Int32, Node>();
        bf.Add(SelectKeys.Select(e => e.ToInt()));

        var list = new List<Node>();
        foreach (var item in SelectKeys)
        {
            var node = ProjectScopeRules.FilterVisible(bf.FindByKey(item.ToInt()));
            if (node != null)
            {
                node.OSKind = OSKindHelper.Parse(node.OS, node.OSVersion);
                if (node.Frameworks.IsNullOrEmpty() || node.Framework.Contains(','))
                {
                    node.Frameworks = node.Framework;
                    node.Framework = node.Frameworks?.Split(',').LastOrDefault();
                }

                //node.Update();
                list.Add(node);
            }
        }
        list.Update(true);

        return JsonRefresh("操作成功！");
    }

    /// <summary>框架推荐。获取当前节点的.NET运行时推荐升级列表</summary>
    /// <param name="id">节点ID</param>
    /// <returns></returns>
    [EntityAuthorize(PermissionFlags.Update)]
    public ActionResult FrameworkRecommend(Int32 id)
    {
        var node = ProjectScopeRules.FilterVisible(Node.FindByID(id));
        if (node == null) return Content(L.T("Node does not exist."));

        ViewBag.Node = node;
        ViewBag.Recs = DotNetPackage.GetRecommendations(node);
        return View("_Node_FrameworkRec");
    }

    /// <summary>刷新DNS。触发指定节点的动态域名解析更新</summary>
    /// <param name="id">节点ID</param>
    /// <returns></returns>
    [EntityAuthorize(PermissionFlags.Update)]
    public async Task<ActionResult> RefreshDns(Int32 id)
    {
        var node = ProjectScopeRules.FilterVisible(FindByID(id));
        if (node == null) throw new InvalidOperationException(L.T("Node does not exist."));

        await _dnsService.RefreshNodeDomainsAsync(node);

        return JsonRefresh("DNS刷新操作已触发！");
    }
}
