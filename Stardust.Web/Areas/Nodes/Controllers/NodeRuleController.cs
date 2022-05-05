using NewLife.Cube;
using NewLife.Web;
using Stardust.Data.Nodes;

namespace Stardust.Web.Areas.Nodes.Controllers
{
    [NodesArea]
    public class NodeRuleController : EntityController<NodeRule>
    {
        static NodeRuleController()
        {
            LogOnChange = true;

            ListFields.RemoveField("Remark");

            {
                var df = ListFields.AddListField("Log", "CreateUserID");
                df.DisplayName = "修改日志";
                df.Header = "修改日志";
                df.Url = "/Admin/Log?category=节点规则&linkId={Id}";
            }
        }

        protected override IEnumerable<NodeRule> Search(Pager p)
        {
            //var appId = p["appId"].ToInt(-1);

            var start = p["dtStart"].ToDateTime();
            var end = p["dtEnd"].ToDateTime();

            return NodeRule.Search(start, end, p["Q"], p);
        }
    }
}