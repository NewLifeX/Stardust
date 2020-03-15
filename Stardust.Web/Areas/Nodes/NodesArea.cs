using NewLife.Cube;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;

namespace Stardust.Web.Areas.Nodes
{
    [DisplayName("节点管理")]
    public class NodesArea : AreaBase
    {
        public NodesArea() : base(nameof(NodesArea).TrimEnd("Area")) { }

        static NodesArea() => RegisterArea<NodesArea>();
    }
}
