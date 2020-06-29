using System;
using System.ComponentModel;
using NewLife.Cube;

namespace Stardust.Web.Areas.Nodes
{
    [DisplayName("节点管理")]
    public class NodesArea : AreaBase
    {
        public NodesArea() : base(nameof(NodesArea).TrimEnd("Area")) { }

        static NodesArea() => RegisterArea<NodesArea>();
    }
}
