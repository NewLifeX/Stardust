using System.Linq;
using System.Threading.Tasks;
using NewLife.Cube;
using Stardust.Data;
using Stardust.Data.Configs;

namespace Stardust.Web.Areas.Configs.Controllers
{
    [ConfigsArea]
    public class AppConfigController : EntityController<AppConfig>
    {
        static AppConfigController()
        {
            MenuOrder = 58;

            {
                var df = ListFields.AddDataField("Configs", "Enable");
                df.Header = "配置";
                df.DisplayName = "配置";
                df.Url = "ConfigData?appId={Id}";
            }

            // 异步同步应用
            {
                Task.Run(() => AppConfig.Sync());
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