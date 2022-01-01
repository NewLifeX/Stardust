using System;
using System.Collections.Generic;
using NewLife.Cube;
using NewLife.Web;
using Stardust.Data.Nodes;

namespace Stardust.Web.Areas.Nodes.Controllers
{
    [Menu(58)]
    [NodesArea]
    public class NodeCommandController : EntityController<NodeCommand>
    {
        protected override IEnumerable<NodeCommand> Search(Pager p)
        {
            var nodeId = p["nodeId"].ToInt(-1);
            var command = p["command"];

            var start = p["dtStart"].ToDateTime();
            var end = p["dtEnd"].ToDateTime();

            return NodeCommand.Search(nodeId, command, start, end, p["Q"], p);
        }
    }
}