using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using NewLife;
using NewLife.Cube;
using NewLife.Cube.ViewModels;
using NewLife.Data;
using NewLife.Web;
using Stardust.Data.Nodes;
using Stardust.Models;
using Stardust.Dns;
using Stardust.Server.Services;
using Stardust.Services;
using XCode;
using XCode.Membership;
using XCode.Model;
using static Stardust.Data.Nodes.Node;

namespace Stardust.Web.Areas.Nodes.Controllers;

[Menu(90)]
[NodesArea]
public class NodeController : NodesEntityController<Node>
{
    private readonly StarFactory _starFactory;
    private readonly DnsService _dnsService;

    static NodeController()
    {
        //ListFields.RemoveField("Secret", "ProductCode", "CompileTime", "OSVersion", "Architecture", "Dpi", "Resolution", "Processor", "CpuID", "Uuid", "MachineGuid", "DiskID", "MACS", "InstallPath", "Runtime", "Framework", "ProvinceID");

        var list = ListFields;
        list.Clear();
        var allows = new[] { "ID", "ProjectName", "Name", "Code", "Category", "ProductCode", "CityName", "Enable", "Version", "OSKind", "Runtime", "Framework", "IP", "OS", "MachineName", "Cpu", "Memory", "TotalSize", "Logins", "LastActive", "OnlineTime", "UpdateTime", "UpdateIP" };
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
            df.DisplayName = "日志";
            df.Url = "/Admin/Log?category=节点&linkId={ID}";
            df.Target = "_frame";
        }

        // 表单字段：Domains添加到参数设置组
        AddFormFields.AddField("Domains");
        EditFormFields.AddField("Domains");
        SearchFields.AddField("Domains");
    }

    public NodeController(StarFactory starFactory, DnsService dnsService)
    {
        LogOnChange = true;

        _starFactory = starFactory;
        _dnsService = dnsService;
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
            var old = Node.FindByID(entity.ID);
            if (old != null && old.Domains != entity.Domains)
            {
                entity.WriteHistory("修改域名", true, $"域名变更：{old.Domains} -> {entity.Domains}", ManageProvider.UserHost);
            }
        }

        return base.OnUpdate(entity);
    }

    protected override IEnumerable<Node> Search(Pager p)
    {
        var nodeId = p["Id"].ToInt(-1);
        if (nodeId > 0)
        {
            var node = Node.FindByKey(nodeId);
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
        var node = FindByID(id);
        if (node != null)
        {
            //NodeCommand.Add(node, "截屏");
            //NodeCommand.Add(node, "抓日志");

            await _starFactory.SendNodeCommandAsync(node.Code, "截屏", cancellationToken: HttpContext.RequestAborted);
            await _starFactory.SendNodeCommandAsync(node.Code, "抓日志", cancellationToken: HttpContext.RequestAborted);
        }

        return RedirectToAction("Index");
    }

    [EntityAuthorize(PermissionFlags.Update)]
    public ActionResult SetAlarm(Boolean enable = true)
    {
        foreach (var item in SelectKeys)
        {
            var dt = FindByID(item.ToInt());
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
            var dt = FindByID(item.ToInt());
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
            var node = bf.FindByKey(item.ToInt());
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

    /// <summary>刷新DNS。触发指定节点的动态域名解析更新</summary>
    /// <param name="id">节点ID</param>
    /// <returns></returns>
    [EntityAuthorize(PermissionFlags.Update)]
    public async Task<ActionResult> RefreshDns(Int32 id)
    {
        var node = FindByID(id);
        if (node == null) throw new InvalidOperationException("节点不存在");

        await _dnsService.RefreshNodeDomainsAsync(node);

        return JsonRefresh("DNS刷新操作已触发！");
    }
}