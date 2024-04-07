using System.ComponentModel;
using NewLife;
using NewLife.Cube;

namespace Stardust.Web.Areas.Nodes;

[DisplayName("节点管理")]
[Menu(888, true, LastUpdate = "20240407")]
public class NodesArea : AreaBase
{
    public NodesArea() : base(nameof(NodesArea).TrimEnd("Area")) { }

    static NodesArea() => RegisterArea<NodesArea>();
}
