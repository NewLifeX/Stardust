using Microsoft.AspNetCore.Mvc.Filters;
using NewLife;
using NewLife.Cube;
using NewLife.Cube.Extensions;
using NewLife.Cube.ViewModels;
using NewLife.Web;
using Stardust.Data.Configs;

namespace Stardust.Web.Areas.Configs.Controllers;

[Menu(0, false)]
[ConfigsArea]
public class ConfigHistoryController : ReadOnlyEntityController<ConfigHistory>
{
    static ConfigHistoryController()
    {
        // 日志列表需要显示详细信息
        ListFields.AddDataField("Remark", null, "TraceId");

        //{
        //    var df = ListFields.GetField("TraceId") as ListField;
        //    df.DisplayName = "跟踪";
        //    df.Url = StarHelper.BuildUrl("{TraceId}");
        //    df.DataVisible = e => e is ConfigHistory entity && !entity.TraceId.IsNullOrEmpty();
        //}
        ListFields.TraceUrl();
    }

    public override void OnActionExecuting(ActionExecutingContext filterContext)
    {
        base.OnActionExecuting(filterContext);

        var appId = GetRequest("appId").ToInt(-1);
        var configId = GetRequest("configId").ToInt(-1);
        if (configId > 0 || appId > 0)
        {
            PageSetting.NavView = "_App_Nav";
            PageSetting.EnableNavbar = false;
        }
    }

    protected override FieldCollection OnGetFields(ViewKinds kind, Object model)
    {
        var fields = base.OnGetFields(kind, model);

        if (kind == ViewKinds.List)
        {
            var configId = GetRequest("configId").ToInt(-1);
            if (configId > 0) fields.RemoveField("ConfigName");
        }

        return fields;
    }

    protected override IEnumerable<ConfigHistory> Search(Pager p)
    {
        var configId = p["configId"].ToInt(-1);
        var action = p["action"];
        var success = p["success"]?.ToBoolean();

        var start = p["dtStart"].ToDateTime();
        var end = p["dtEnd"].ToDateTime();

        return ConfigHistory.Search(configId, action, success, start, end, p["Q"], p);
    }
}