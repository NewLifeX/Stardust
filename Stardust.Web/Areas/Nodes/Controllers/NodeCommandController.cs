using NewLife;
using NewLife.Cube;
using NewLife.Cube.Extensions;
using NewLife.Cube.ViewModels;
using NewLife.Web;
using Stardust.Data.Nodes;

namespace Stardust.Web.Areas.Nodes.Controllers
{
    [Menu(58)]
    [NodesArea]
    public class NodeCommandController : EntityController<NodeCommand>
    {
        static NodeCommandController()
        {
            {
                var df = ListFields.GetField("TraceId") as ListField;
                df.DisplayName = "跟踪";
                df.Url = StarHelper.BuildUrl("{TraceId}");
                df.DataVisible = (e, f) => e is NodeCommand entity && !entity.TraceId.IsNullOrEmpty();
            }
        }

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