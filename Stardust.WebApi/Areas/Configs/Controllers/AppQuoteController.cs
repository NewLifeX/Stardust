using NewLife;
using NewLife.Cube;
using NewLife.Cube.ViewModels;
using NewLife.Web;
using Stardust.Data.Configs;

namespace Stardust.Web.Areas.Configs.Controllers;

/// <summary>应用依赖。应用可以引用依赖另一个应用</summary>
[Menu(30, true, Icon = "fa-table")]
[ConfigsArea]
public class AppQuoteController : ConfigsEntityController<AppQuote>
{
    static AppQuoteController()
    {
        ListFields.RemoveCreateField().RemoveRemarkField();

        ListFields.TraceUrl();
    }

    /// <summary>高级搜索。按条件分页查询</summary>
    /// <param name="p">分页参数</param>
    /// <returns>实体列表</returns>
    protected override IEnumerable<AppQuote> Search(Pager p)
    {
        var appId = p["appId"].ToInt(-1);
        var targetAppId = p["targetAppId"].ToInt(-1);

        var start = p["dtStart"].ToDateTime();
        var end = p["dtEnd"].ToDateTime();

        return AppQuote.Search(appId, targetAppId, start, end, p["Q"], p);
    }
}
