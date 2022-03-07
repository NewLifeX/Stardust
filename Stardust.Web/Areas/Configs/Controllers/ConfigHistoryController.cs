using NewLife;
using NewLife.Cube;
using NewLife.Cube.Extensions;
using NewLife.Cube.ViewModels;
using NewLife.Web;
using Stardust.Data.Configs;

namespace Stardust.Web.Areas.Configs.Controllers
{
    [ConfigsArea]
    public class ConfigHistoryController : ReadOnlyEntityController<ConfigHistory>
    {
        static ConfigHistoryController()
        {
            // 日志列表需要显示详细信息
            ListFields.AddDataField("Remark", null, "Action");

            {
                var df = ListFields.GetField("TraceId") as ListField;
                df.DisplayName = "跟踪";
                df.Url = StarHelper.BuildUrl("{TraceId}");
                df.DataVisible = (e, f) => e is ConfigHistory entity && !entity.TraceId.IsNullOrEmpty();
            }
        }

        protected override IEnumerable<ConfigHistory> Search(Pager p)
        {
            var appId = p["appId"].ToInt(-1);
            var action = p["action"];
            var success = p["success"]?.ToBoolean();

            var start = p["dtStart"].ToDateTime();
            var end = p["dtEnd"].ToDateTime();

            return ConfigHistory.Search(appId, action, success, start, end, p["Q"], p);
        }
    }
}