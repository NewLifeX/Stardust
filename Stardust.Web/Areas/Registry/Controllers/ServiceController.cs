using NewLife;
using NewLife.Cube;
using NewLife.Cube.ViewModels;
using NewLife.Web;
using Stardust.Data;
using XCode.Membership;

namespace Stardust.Web.Areas.Registry.Controllers;

/// <summary>服务信息。服务提供者发布的服务</summary>
[Menu(40, true, Icon = "fa-table")]
[RegistryArea]
public class ServiceController : EntityController<Service>
{
    static ServiceController()
    {
        LogOnChange = true;
        ListFields.RemoveCreateField();

        ListFields.RemoveField("Secret", "HealthAddress");
        ListFields.RemoveCreateField()
            .RemoveUpdateField()
            .RemoveRemarkField();

        {
            var df = ListFields.GetField("Providers") as ListField;
            df.DisplayName = "{Providers}";
            df.Url = "/Registry/AppService?serviceId={Id}";
        }
        {
            var df = ListFields.GetField("Consumers") as ListField;
            df.DisplayName = "{Consumers}";
            df.Url = "/Registry/AppConsume?serviceId={Id}";
        }
    }

    /// <summary>高级搜索。列表页查询、导出Excel、导出Json、分享页等使用</summary>
    /// <param name="p">分页器。包含分页排序参数，以及Http请求参数</param>
    /// <returns></returns>
    protected override IEnumerable<Service> Search(Pager p)
    {
        //var deviceId = p["deviceId"].ToInt(-1);
        var name = p["name"];

        var start = p["dtStart"].ToDateTime();
        var end = p["dtEnd"].ToDateTime();

        return Service.Search(name, start, end, p["Q"], p);
    }
}