using System;
using System.Collections.Generic;
using NewLife.Cube;
using NewLife.Web;
using Stardust.Data.Nodes;

namespace Stardust.Web.Areas.Nodes.Controllers
{
    [NodesArea]
    public class NodeDataController : EntityController<NodeData>
    {
        static NodeDataController() => MenuOrder = 60;

        protected override IEnumerable<NodeData> Search(Pager p)
        {
            var nodeId = p["nodeId"].ToInt(-1);

            var start = p["dtStart"].ToDateTime();
            var end = p["dtEnd"].ToDateTime();

            return NodeData.Search(nodeId, start, end, p["Q"], p);
        }
    }
}