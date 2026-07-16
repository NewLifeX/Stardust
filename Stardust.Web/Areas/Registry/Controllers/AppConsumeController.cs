using NewLife;
using NewLife.Cube;
using NewLife.Cube.ViewModels;
using NewLife.Web;
using Stardust.Data;
using XCode.Membership;

namespace Stardust.Web.Areas.Registry.Controllers;

[RegistryArea]
[Menu(0, false)]
public class AppConsumeController : RegistryEntityController<AppConsume>
{
    static AppConsumeController()
    {
        ListFields.RemoveField("ServiceId");

        {
            var df = ListFields.GetField("ServiceName") as ListField;
            df.DisplayName = "{ServiceName}";
            df.Url = "/Registry/Service?name={ServiceName}";
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
            var df = ListFields.GetField("Client") as ListField;
            df.Url = "/Registry/AppConsume?appId={AppId}&client={Client}";
        }
    }

    /// <summary>高级搜索。按条件分页查询</summary>
    /// <param name="p">分页参数</param>
    /// <returns>实体列表</returns>
    protected override IEnumerable<AppConsume> Search(Pager p)
    {
        //PageSetting.EnableAdd = false;
        //PageSetting.EnableNavbar = false;

        var appId = p["appId"].ToInt(-1);
        var client = p["client"];
        var serviceId = p["serviceId"].ToInt(-1);
        var enable = p["enable"]?.ToBoolean();

        return AppConsume.Search(appId, serviceId, client, enable, p["Q"], p);
    }
}
