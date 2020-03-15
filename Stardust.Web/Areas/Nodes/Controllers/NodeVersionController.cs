using NewLife.Cube;
using Stardust.Data.Nodes;

namespace Stardust.Web.Areas.Nodes.Controllers
{
    [NodesArea]
    public class NodeVersionController : EntityController<NodeVersion>
    {
        static NodeVersionController() => MenuOrder = 89;
    }
}