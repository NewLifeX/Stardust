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
    }
}