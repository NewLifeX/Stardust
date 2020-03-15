using System;
using System.Collections.Generic;
using NewLife.Cube;
using NewLife.Web;
using Stardust.Data.Nodes;

namespace Stardust.Web.Areas.Nodes.Controllers
{
    [NodesArea]
    public class NodeHistoryController : EntityController<NodeHistory>
    {
        static NodeHistoryController() => MenuOrder = 60;

        protected override IEnumerable<NodeHistory> Search(Pager p)
        {
            var rids = p["areaId"].SplitAsInt("/");
            var provinceId = rids.Length > 0 ? rids[0] : -1;
            var cityId = rids.Length > 1 ? rids[1] : -1;

            var nodeId = p["nodeId"].ToInt(-1);
            var action = p["action"];
            var success = p["success"]?.ToBoolean();

            var start = p["dtStart"].ToDateTime();
            var end = p["dtEnd"].ToDateTime();

            return NodeHistory.Search(nodeId, provinceId, cityId, action, success, start, end, p["Q"], p);
        }
    }
}