using NewLife;
using NewLife.Cube;
using NewLife.Web;
using Stardust.Data.Nodes;

namespace Stardust.Web.Areas.Nodes.Controllers;

[Menu(10)]
[NodesArea]
public class NodeRuleController : EntityController<NodeRule>
{
    static NodeRuleController()
    {
        LogOnChange = true;

        ListFields.RemoveField("Remark");

        {
            var df = ListFields.AddListField("Log", "CreateUserID");
            df.DisplayName = "审计日志";
            df.Header = "审计日志";
            df.Url = "/Admin/Log?category=节点规则&linkId={Id}";
            df.Target = "_frame";
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