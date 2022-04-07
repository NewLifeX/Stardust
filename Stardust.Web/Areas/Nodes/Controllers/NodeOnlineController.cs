using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using NewLife;
using NewLife.Cube;
using NewLife.Cube.ViewModels;
using NewLife.Web;
using Stardust.Data.Nodes;

namespace Stardust.Web.Areas.Nodes.Controllers
{
    [Menu(70)]
    [NodesArea]
    public class NodeOnlineController : ReadOnlyEntityController<NodeOnline>
    {
        private readonly StarFactory _starFactory;

        static NodeOnlineController()
        {
            //ListFields.RemoveField("SessionID", "IP", "ProvinceID", "CityID", "Macs", "Token");

            var list = ListFields;
            list.Clear();
            var allows = new[] { "ID", "Name", "Category", "CityName", "PingCount", "WebSocket", "Version", "IP", "AvailableMemory", "AvailableFreeSpace", "CpuRate", "ProcessCount", "UplinkSpeed", "DownlinkSpeed", "LocalTime", "CreateTime", "CreateIP", "UpdateTime" };
            foreach (var item in allows)
            {
                list.AddListField(item);
            }

            {
                var df = ListFields.GetField("Name") as ListField;
                df.DisplayName = "{Name}";
                df.Url = "Node?Id={NodeID}";
            }
            //{
            //    var df = ListFields.AddListField("History", "Version");
            //    df.DisplayName = "历史";
            //    df.Url = "NodeHistory?nodeId={NodeID}";
            //}
            {
                var df = ListFields.AddListField("Meter", "Version");
                df.DisplayName = "性能";
                df.Url = "NodeData?nodeId={NodeID}";
            }
            {
                var df = ListFields.AddListField("App", "Version");
                df.DisplayName = "应用实例";
                df.Url = "/Registry/AppOnline?nodeId={NodeID}";
            }
            //{
            //    var df = ListFields.AddListField("Log", "Version");
            //    df.DisplayName = "日志";
            //    df.Url = "/Admin/Log?category=节点&linkId={NodeID}";
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