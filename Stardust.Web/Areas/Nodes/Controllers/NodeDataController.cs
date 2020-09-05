using System;
using System.Collections.Generic;
using NewLife;
using NewLife.Cube;
using NewLife.Web;
using Stardust.Data.Nodes;
using XCode;

namespace Stardust.Web.Areas.Nodes.Controllers
{
    [NodesArea]
    public class NodeDataController : EntityController<NodeData>
    {
        static NodeDataController()
        {
            MenuOrder = 60;

            ListFields.RemoveField("Id");
        }

        protected override IEnumerable<NodeData> Search(Pager p)
        {
            var nodeId = p["nodeId"].ToInt(-1);

            var start = p["dtStart"].ToDateTime();
            var end = p["dtEnd"].ToDateTime();

            if (p.Sort.IsNullOrEmpty()) p.OrderBy = NodeData._.ID.Desc();

            return NodeData.Search(nodeId, start, end, p["Q"], p);
        }
    }
}