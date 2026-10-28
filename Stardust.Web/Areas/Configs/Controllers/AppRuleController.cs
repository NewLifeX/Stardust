using NewLife.Cube;
using Stardust.Data.Configs;

namespace Stardust.Web.Areas.Configs.Controllers;

/// <summary>应用规则。控制应用可访问的配置范围</summary>
[Menu(30)]
[ConfigsArea]
public class AppRuleController : ConfigsEntityController<AppRule>
{
    static AppRuleController()
    {
        LogOnChange = true;

        {
            var df = ListFields.AddListField("Log", "CreateUserID");
            df.DisplayName = "审计日志";
            df.Header = "审计日志";
            df.Url = "/Admin/Log?category=应用规则&linkId={Id}";
            df.Target = "_frame";
        }
    }

    //protected override IEnumerable<AppRule> Search(Pager p)
    //{
    //    var appId = p["appId"].ToInt(-1);

    //    var start = p["dtStart"].ToDateTime();
    //    var end = p["dtEnd"].ToDateTime();

    //    return AppRule.Search(appId, start, end, p["Q"], p);
    //}
}