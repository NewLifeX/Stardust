using System.ComponentModel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using NewLife;
using NewLife.Cube;
using NewLife.Cube.Extensions;
using NewLife.Cube.ViewModels;
using NewLife.Web;
using Stardust.Data.Nodes;
using XCode.Membership;
using static Stardust.Data.Nodes.NodeOnline;
using Node = Stardust.Data.Nodes.Node;

namespace Stardust.Web.Areas.Nodes.Controllers;

[Menu(80)]
[NodesArea]
public class NodeOnlineController : ReadOnlyEntityController<NodeOnline>
{
    private readonly StarFactory _starFactory;

    static NodeOnlineController()
    {
        //ListFields.RemoveField("SessionID", "IP", "ProvinceID", "CityID", "Macs", "Token");

        var list = ListFields;
        list.Clear();
        var allows = new[] { "ID", "ProjectName", "Name", "Category", "ProductCode", "CityName", "Address", "PingCount", "WebSocket", "Version", "OSKind", "IP", "AvailableMemory", "MemoryUsed", "AvailableFreeSpace", "SpaceUsed", "CpuRate", "ProcessCount", __.Signal, __.Offset, "UplinkSpeed", "DownlinkSpeed", "IntranetScore", "InternetScore", "TraceId", "LocalTime", "CreateTime", "UpdateTime", "UpdateIP" };
        foreach (var item in allows)
        {
            list.AddListField(item);
        }

        {
            var df = ListFields.GetField("Name") as ListField;
            df.DisplayName = "{Name}";
            df.Url = "/Nodes/Node/Detail?Id={NodeID}";
            df.Target = "_blank";
        }
        //{
        //    var df = ListFields.AddListField("Meter", "Version");
        //    df.DisplayName = "性能";
        //    df.Url = "/Nodes/NodeData?nodeId={NodeID}";
        //}
        //{
        //    var df = ListFields.AddListField("App", "Version");
        //    df.DisplayName = "应用实例";
        //    df.Url = "/Registry/AppOnline?nodeId={NodeID}";
        //}

        ListFields.TraceUrl();
    }

    public NodeOnlineController(StarFactory starFactory) => _starFactory = starFactory;

    public override void OnActionExecuting(ActionExecutingContext filterContext)
    {
        base.OnActionExecuting(filterContext);

        var nodeId = GetRequest("nodeId").ToInt(-1);
        if (nodeId > 0)
        {
            PageSetting.NavView = "_Node_Nav";
            PageSetting.EnableNavbar = false;
        }

        var projectId = GetRequest("projectId").ToInt(-1);
        if (projectId > 0)
        {
            PageSetting.NavView = "_Project_Nav";
            PageSetting.EnableNavbar = false;
        }
    }

    protected override FieldCollection OnGetFields(ViewKinds kind, Object model)
    {
        var fields = base.OnGetFields(kind, model);

        if (kind == ViewKinds.List)
        {
            var nodeId = GetRequest("nodeId").ToInt(-1);
            if (nodeId > 0) fields.RemoveField("NodeName");
        }

        return fields;
    }

    protected override IEnumerable<NodeOnline> Search(Pager p)
    {
        var nodeId = p["nodeId"].ToInt(-1);
        var rids = p["areaId"].SplitAsInt("/");
        var provinceId = rids.Length > 0 ? rids[0] : -1;
        var cityId = rids.Length > 1 ? rids[1] : -1;

        var projectId = p["projectId"].ToInt(-1);
        var category = p["category"];
        var start = p["dtStart"].ToDateTime();
        var end = p["dtEnd"].ToDateTime();

        PageSetting.EnableSelect = true;
        p.RetrieveState = true;

        return NodeOnline.Search(projectId, nodeId, provinceId, cityId, category, start, end, p["Q"], p);
    }

    public async Task<ActionResult> Trace(Int32 id)
    {
        var node = Node.FindByID(id);
        if (node != null)
        {
            //NodeCommand.Add(node, "截屏");
            //NodeCommand.Add(node, "抓日志");

            await _starFactory.SendNodeCommand(node.Code, "截屏");
            await _starFactory.SendNodeCommand(node.Code, "抓日志");
        }

        return RedirectToAction("Index");
    }

    [DisplayName("检查更新")]
    [EntityAuthorize((PermissionFlags)16)]
    public async Task<ActionResult> CheckUpgrade()
    {
        var ts = new List<Task<Int32>>();
        foreach (var item in SelectKeys)
        {
            var online = NodeOnline.FindById(item.ToInt());
            if (online?.Node != null)
            {
                ts.Add(_starFactory.SendNodeCommand(online.Node.Code, "node/upgrade", null, 0, 600, 0));
            }
        }

        var rs = await Task.WhenAll(ts);

        return JsonRefresh($"操作成功！下发指令{rs.Length}个，成功{rs.Count(e => e > 0)}个");
    }

    [DisplayName("同步时间")]
    [EntityAuthorize((PermissionFlags)16)]
    public async Task<ActionResult> SyncTime()
    {
        var ts = new List<Task<Int32>>();
        foreach (var item in SelectKeys)
        {
            var online = NodeOnline.FindById(item.ToInt());
            if (online?.Node != null)
            {
                ts.Add(_starFactory.SendNodeCommand(online.Node.Code, "node/syncTime", null, 0, 600, 0));
            }
        }

        var rs = await Task.WhenAll(ts);

        return JsonRefresh($"操作成功！下发指令{rs.Length}个，成功{rs.Count(e => e > 0)}个");
    }

    [DisplayName("重启服务")]
    [EntityAuthorize((PermissionFlags)32)]
    public async Task<ActionResult> Restart()
    {
        var ts = new List<Task<Int32>>();
        foreach (var item in SelectKeys)
        {
            var online = NodeOnline.FindById(item.ToInt());
            if (online?.Node != null)
            {
                ts.Add(_starFactory.SendNodeCommand(online.Node.Code, "node/restart", null, 0, 600, 0));
            }
        }

        var rs = await Task.WhenAll(ts);

        return JsonRefresh($"操作成功！下发指令{rs.Length}个，成功{rs.Count(e => e > 0)}个");
    }

    [DisplayName("重启系统")]
    [EntityAuthorize((PermissionFlags)64)]
    public async Task<ActionResult> Reboot()
    {
        var ts = new List<Task<Int32>>();
        foreach (var item in SelectKeys)
        {
            var online = NodeOnline.FindById(item.ToInt());
            if (online?.Node != null)
            {
                ts.Add(_starFactory.SendNodeCommand(online.Node.Code, "node/reboot", null, 0, 600, 0));
            }
        }

        var rs = await Task.WhenAll(ts);

        return JsonRefresh($"操作成功！下发指令{rs.Length}个，成功{rs.Count(e => e > 0)}个");
    }

    [DisplayName("执行命令")]
    [EntityAuthorize((PermissionFlags)16)]
    public async Task<ActionResult> Execute(String command, String argument)
    {
        if (GetRequest("keys") == null) throw new ArgumentNullException(nameof(SelectKeys));
        if (command.IsNullOrEmpty()) throw new ArgumentNullException(nameof(command));

        var ts = new List<Task<Int32>>();
        foreach (var item in SelectKeys)
        {
            var online = NodeOnline.FindById(item.ToInt());
            if (online != null && online.Node != null)
            {
                ts.Add(_starFactory.SendNodeCommand(online.Node.Code, command, argument, 0, 300, 0));
            }
        }

        var rs = await Task.WhenAll(ts);

        return JsonRefresh($"操作成功！下发指令{rs.Length}个，成功{rs.Count(e => e > 0)}个");
    }
}