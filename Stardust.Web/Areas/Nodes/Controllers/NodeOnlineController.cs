using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using NewLife;
using NewLife.Cube;
using NewLife.Web;
using Stardust.Data.Nodes;

namespace Stardust.Web.Areas.Nodes.Controllers
{
    [NodesArea]
    public class NodeOnlineController : ReadOnlyEntityController<NodeOnline>
    {
        private readonly StarFactory _starFactory;

        static NodeOnlineController()
        {
            MenuOrder = 70;

            ListFields.RemoveField("SessionID", "IP", "ProvinceID", "CityID", "Macs", "Token");

            //{
            //    var df = ListFields.AddDataField("Category");
            //    df.Header = "分类";
            //    df.DisplayName = "{Category}";
            //    df.Url = "?category={Category}";
            //}
            //{
            //    var df = ListFields.AddDataField("NodeName");
            //    df.Header = "设备";
            //    df.Url = "Node?id={NodeID}";
            //}
            //{
            //    var df = ListFields.AddDataField("NodeData", null, "NodeName");
            //    df.DisplayName = "数据";
            //    df.Url = "NodeData?nodeId={NodeID}";
            //}
            //{
            //    var df = ListFields.AddDataField("Temperature");
            //    df.DisplayName = "{Temperature}°C";
            //}
        }
       
        public NodeOnlineController(StarFactory starFactory) => _starFactory = starFactory;

        protected override IEnumerable<NodeOnline> Search(Pager p)
        {
            var nodeId = p["nodeId"].ToInt(-1);
            var rids = p["areaId"].SplitAsInt("/");
            var provinceId = rids.Length > 0 ? rids[0] : -1;
            var cityId = rids.Length > 1 ? rids[1] : -1;

            var category = p["category"];
            var start = p["dtStart"].ToDateTime();
            var end = p["dtEnd"].ToDateTime();

            return NodeOnline.Search(nodeId, provinceId, cityId, category, start, end, p["Q"], p);
        }

        public async Task<ActionResult> Trace(Int32 id)
        {
            var node = Node.FindByID(id);
            if (node != null)
            {
                //NodeCommand.Add(node, "截屏");
                //NodeCommand.Add(node, "抓日志");

                await _starFactory.SendNodeCommand(node.Code, "截屏");
                await _starFactory.SendNodeCommand(node.Code, "抓日志");
            }

            return RedirectToAction("Index");
        }
    }
}