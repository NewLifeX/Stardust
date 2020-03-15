using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using NewLife.Cube;
using NewLife.Web;
using Stardust.Data.Nodes;

namespace Stardust.Web.Areas.Nodes.Controllers
{
    [NodesArea]
    public class NodeController : EntityController<Node>
    {
        static NodeController() => MenuOrder = 80;

        protected override IEnumerable<Node> Search(Pager p)
        {
            var nodeId = p["Id"].ToInt(-1);
            if (nodeId > 0)
            {
                var node = Node.FindByID(nodeId);
                if (node != null) return new[] { node };
            }

            var rids = p["areaId"].SplitAsInt("/");
            var provinceId = rids.Length > 0 ? rids[0] : -1;
            var cityId = rids.Length > 1 ? rids[1] : -1;

            var version = p["version"];
            var enable = p["enable"]?.ToBoolean();

            var start = p["dtStart"].ToDateTime();
            var end = p["dtEnd"].ToDateTime();

            return Node.Search(provinceId, cityId, version, enable, start, end, p["Q"], p);
        }

        public ActionResult Trace(Int32 id)
        {
            var node = Node.FindByID(id);
            if (node != null)
            {
                NodeCommand.Add(node, "截屏");
                NodeCommand.Add(node, "抓日志");
            }

            return RedirectToAction("Index");
        }
    }
}