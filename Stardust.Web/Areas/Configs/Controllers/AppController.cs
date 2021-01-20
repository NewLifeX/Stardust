using NewLife.Cube;
using Stardust.Data;

namespace Stardust.Web.Areas.Configs.Controllers
{
    [ConfigsArea]
    public class AppController : EntityController<App>
    {
        static AppController()
        {
            MenuOrder = 58;

            {
                var df = ListFields.AddDataField("Configs", "Enable");
                df.Header = "配置";
                df.DisplayName = "配置";
                df.Url = "ConfigData?appId={ID}";
            }
        }

        //protected override IEnumerable<AppConfig> Search(Pager p)
        //{
        //    var appId = p["appId"].ToInt(-1);

        //    var start = p["dtStart"].ToDateTime();
        //    var end = p["dtEnd"].ToDateTime();

        //    return AppConfig.Search(appId, start, end, p["Q"], p);
        //}
    }
}