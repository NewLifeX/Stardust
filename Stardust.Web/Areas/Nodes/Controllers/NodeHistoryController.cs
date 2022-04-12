using NewLife;
using NewLife.Cube;
using NewLife.Cube.Extensions;
using NewLife.Cube.ViewModels;
using NewLife.Web;
using Stardust.Data.Nodes;
using XCode;

namespace Stardust.Web.Areas.Nodes.Controllers
{
    [Menu(60)]
    [NodesArea]
    public class NodeHistoryController : ReadOnlyEntityController<NodeHistory>
    {
        static NodeHistoryController()
        {
            ListFields.RemoveField("ID", "ProvinceName", "Version", "CompileTime");
            ListFields.AddListField("Remark", null, "Success");

            {
                var df = ListFields.GetField("TraceId") as ListField;
                df.DisplayName = "跟踪";
                df.Url = StarHelper.BuildUrl("{TraceId}");
                df.DataVisible = (e, f) => e is NodeHistory entity && !entity.TraceId.IsNullOrEmpty();
            }

            {
                var df = ListFields.GetField("Action") as ListField;
                df.Url = "?nodeId={NodeId}&action={Action}";
            }
        }

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

            if (p.Sort.IsNullOrEmpty()) p.OrderBy = NodeHistory._.Id.Desc();

            return NodeHistory.Search(nodeId, provinceId, cityId, action, success, start, end, p["Q"], p);
        }
    }
}