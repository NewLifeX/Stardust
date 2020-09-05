using System;
using System.Collections.Generic;
using NewLife;
using NewLife.Cube;
using NewLife.Web;
using Stardust.Data.Nodes;

namespace Stardust.Web.Areas.Nodes.Controllers
{
    [NodesArea]
    public class NodeOnlineController : ReadOnlyEntityController<NodeOnline>
    {
        static NodeOnlineController() => MenuOrder = 70;

        protected override IEnumerable<NodeOnline> Search(Pager p)
        {
            var nodeId = p["nodeId"].ToInt(-1);
            var rids = p["areaId"].SplitAsInt("/");
            var provinceId = rids.Length > 0 ? rids[0] : -1;
            var cityId = rids.Length > 1 ? rids[1] : -1;

            var start = p["dtStart"].ToDateTime();
            var end = p["dtEnd"].ToDateTime();

            return NodeOnline.Search(nodeId, provinceId, cityId, start, end, p["Q"], p);
        }
    }
}