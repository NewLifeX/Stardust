using System.ComponentModel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using NewLife;
using NewLife.Cube;
using NewLife.Cube.Extensions;
using NewLife.Cube.ViewModels;
using NewLife.Web;
using Stardust.Data;
using XCode.Membership;

namespace Stardust.Web.Areas.Registry.Controllers;

[RegistryArea]
[Menu(95)]
public class AppOnlineController : EntityController<AppOnline>
{
    private readonly StarFactory _starFactory;

    static AppOnlineController()
    {
        ListFields.RemoveField("ProjectName", "ProcessName", "MachineName", "UserName", "Token");

        {
            var df = ListFields.GetField("AppName") as ListField;
            df.DisplayName = "{AppName}";
            df.Url = "/Registry/App/Detail?id={AppId}";
            df.Target = "_blank";
        }
        {
            var df = ListFields.GetField("NodeName") as ListField;
            df.Header = "节点";
            df.DisplayName = "{NodeName}";
            df.Url = "/Nodes/Node?Id={NodeId}";
            df.Target = "_blank";
        }
        {
            var df = ListFields.AddListField("Meter", "WebSocket");
            df.Header = "性能";
            df.DisplayName = "性能";
            df.Url = "/Registry/AppMeter?appId={AppId}&clientId={Client}";
        }
        {
            var df = ListFields.AddListField("History", "WebSocket");
            df.DisplayName = "历史";
            df.Url = "/Registry/AppHistory?appId={AppId}&client={Client}";
        }
        //{
        //    var df = ListFields.GetField("TraceId") as ListField;
        //    df.DisplayName = "跟踪";
        //    df.Url = StarHelper.BuildUrl("{TraceId}");
        //    df.DataVisible = e => e is AppOnline entity && !entity.TraceId.IsNullOrEmpty();
        //}
        ListFields.TraceUrl();
    }

    public AppOnlineController(StarFactory starFactory) => _starFactory = starFactory;

    public override void OnActionExecuting(ActionExecutingContext filterContext)
    {
        base.OnActionExecuting(filterContext);

        PageSetting.EnableAdd = false;

        var appId = GetRequest("appId").ToInt(-1);
        if (appId > 0)
        {
            PageSetting.NavView = "_App_Nav";
            PageSetting.EnableNavbar = false;
        }

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

            var appId = GetRequest("appId").ToInt(-1);
            if (appId > 0) fields.RemoveField("AppName", "Category", "Name");
        }

        return fields;
    }

    protected override IEnumerable<AppOnline> Search(Pager p)
    {
        var appId = p["appId"].ToInt(-1);
        var nodeId = p["nodeId"].ToInt(-1);
        var projectId = p["projectId"].ToInt(-1);
        var category = p["category"];

        var start = p["dtStart"].ToDateTime();
        var end = p["dtEnd"].ToDateTime();

        return AppOnline.Search(projectId, appId, nodeId, category, start, end, p["Q"], p);
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
            var online = AppOnline.FindById(item.ToInt());
            if (online != null && online.App != null)
            {
                ts.Add(_starFactory.SendAppCommand(online.App.Name, online.Client, command, argument, 0, 300, 0));
            }
        }

        var rs = await Task.WhenAll(ts);

        return JsonRefresh($"操作成功！下发指令{rs.Length}个，成功{rs.Count(e => e > 0)}个");
    }

    [DisplayName("释放内存")]
    [EntityAuthorize((PermissionFlags)32)]
    public async Task<ActionResult> FreeMemory()
    {
        var ts = new List<Task<Int32>>();
        foreach (var item in SelectKeys)
        {
            var online = AppOnline.FindById(item.ToInt());
            if (online != null && online.App != null)
            {
                ts.Add(_starFactory.SendAppCommand(online.App.Name, online.Client, "app/freeMemory", null, 0, 300, 0));
            }
        }

        var rs = await Task.WhenAll(ts);

        return JsonRefresh($"操作成功！下发指令{rs.Length}个，成功{rs.Count(e => e > 0)}个");
    }
}