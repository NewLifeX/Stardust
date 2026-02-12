using System.Web;
using Microsoft.AspNetCore.Mvc;
using Stardust.Data.Nodes;
using NewLife;
using NewLife.Cube;
using NewLife.Cube.ViewModels;
using NewLife.Data;
using NewLife.Log;
using NewLife.Web;
using XCode.Membership;
using Stardust.Server.Services;

namespace Stardust.Web.Areas.MySql.Controllers;

[Menu(50)]
[MySqlArea]
public class MySqlNodeController : EntityController<MySqlNode>
{
    private readonly IMySqlService _mySqlService;

    static MySqlNodeController()
    {
        LogOnChange = true;

        ListFields.RemoveField("WebHook", "AlarmConnections", "AlarmQPS", "AlarmSlowQuery");
        ListFields.RemoveCreateField();
        ListFields.RemoveField("UpdateUser", "UpdateUserID", "UpdateIP", "Remark");

        {
            var df = ListFields.GetField("ProjectName") as ListField;
            df.Url = "/Platform/GalaxyProject?projectId={ProjectId}";
            df.Target = "_frame";
        }
        {
            var df = ListFields.AddListField("Monitor", "UpdateTime");
            df.DisplayName = "监控";
            df.Header = "监控";
            df.Url = "/MySql/MySqlData?mysqlId={Id}";
        }
        {
            var df = ListFields.AddListField("Refresh", "UpdateTime");
            df.DisplayName = "刷新";
            df.Header = "刷新";
            df.Url = "/MySql/MySqlNode/Refresh?Id={Id}";
            df.DataAction = "action";
        }
        {
            var df = ListFields.AddListField("Log", "UpdateTime");
            df.DisplayName = "审计日志";
            df.Header = "审计日志";
            df.Url = $"/Admin/Log?category={HttpUtility.UrlEncode("MySql节点")}&linkId={{Id}}";
            df.Target = "_frame";
        }
    }

    public MySqlNodeController(IMySqlService mySqlService) => _mySqlService = mySqlService;

    protected override IEnumerable<MySqlNode> Search(Pager p)
    {
        var nodeId = p["Id"].ToInt(-1);
        if (nodeId > 0)
        {
            var node = MySqlNode.FindById(nodeId);
            if (node != null) return new[] { node };
        }

        var server = p["server"];
        var port = p["port"].ToInt(-1);
        var projectId = p["projectId"].ToInt(-1);
        var enable = p["enable"]?.ToBoolean();

        var start = p["dtStart"].ToDateTime();
        var end = p["dtEnd"].ToDateTime();

        return MySqlNode.Search(server, port, projectId, enable, start, end, p["Q"], p);
    }

    /// <summary>搜索</summary>
    /// <param name="category">分类</param>
    /// <param name="key">关键字</param>
    /// <returns></returns>
    public ActionResult NodeSearch(String category, String key = null)
    {
        var page = new PageParameter { PageSize = 20 };

        // 默认排序
        if (page.Sort.IsNullOrEmpty()) page.Sort = MySqlNode._.Name;

        var list = MySqlNode.Search(null, -1, -1, true, DateTime.MinValue, DateTime.MinValue, key, page);

        return Json(0, null, list.Select(e => new
        {
            e.Id,
            e.Name,
            e.Server,
            e.Port,
            e.Category,
        }).ToArray());
    }

    [EntityAuthorize(PermissionFlags.Update)]
    public ActionResult Refresh(Int32 id)
    {
        var node = MySqlNode.FindById(id);
        if (node != null)
        {
            XTrace.WriteLine("刷新 {0}/{1} {2}:{3}", node.Name, node.Id, node.Server, node.Port);

            try
            {
                _mySqlService.TraceNode(node);

                LogProvider.Provider.WriteLog("MySqlNode", "Refresh", true, $"刷新MySql节点[{node}]成功");
            }
            catch (Exception ex)
            {
                LogProvider.Provider.WriteLog("MySqlNode", "Refresh", false, ex?.GetMessage());

                throw;
            }
        }

        return JsonRefresh($"刷新[{node}]成功！");
    }
}
