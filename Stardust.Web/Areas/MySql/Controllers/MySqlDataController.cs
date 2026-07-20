using Microsoft.AspNetCore.Mvc;
using Stardust.Data.Nodes;
using NewLife;
using NewLife.Cube;
using NewLife.Cube.ViewModels;
using NewLife.Web;

namespace Stardust.Web.Areas.MySql.Controllers;

[MySqlArea]
public class MySqlDataController : ReadOnlyEntityController<MySqlData>
{
    static MySqlDataController()
    {
        ListFields.RemoveCreateField();
        ListFields.RemoveRemarkField();
    }

    /// <summary>高级搜索。按条件分页查询</summary>
    /// <param name="p">分页参数</param>
    /// <returns>实体列表</returns>
    protected override IEnumerable<MySqlData> Search(Pager p)
    {
        var mysqlId = p["mysqlId"].ToInt(-1);

        var start = p["dtStart"].ToDateTime();
        var end = p["dtEnd"].ToDateTime();

        return MySqlData.Search(mysqlId, start, end, p["Q"], p);
    }
}
