using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using NewLife;
using NewLife.Cube;
using NewLife.Cube.Extensions;
using NewLife.Web;
using Stardust.Data.Nodes;
using XCode;
using static Stardust.Data.Nodes.NodeData;

namespace Stardust.Web.Areas.Nodes.Controllers;

/// <summary>节点数据。查看节点上报的详细性能数据（CPU/内存/磁盘/网络），支持时序图表展示</summary>
[Menu(60, false)]
[NodesArea]
public class NodeDataController : NodesEntityController<NodeData>
{
    static NodeDataController()
    {
        ListFields.RemoveField("Id", "*Latency", "*LossRate");
        ListFields.TraceUrl();
    }

    /// <summary>高级搜索。按条件分页查询</summary>
    /// <param name="p">分页参数</param>
    /// <returns>实体列表</returns>
    protected override IEnumerable<NodeData> Search(Pager p)
    {
        var nodeId = p["nodeId"].ToInt(-1);

        var start = p["dtStart"].ToDateTime();
        var end = p["dtEnd"].ToDateTime();

        if (nodeId > 0)
        {
            // 最近10小时
            if (p.PageSize == 20 && nodeId > 0)
            {
                p.PageSize = 24 * 60;

                // 默认查询最近24小时。如果指定了应用，还需要根据节点心跳间隔来调整
                var node = Node.FindByID(nodeId);
                if (node != null && node.Period > 0)
                {
                    p.PageSize = 24 * 3600 / node.Period;
                }
            }
        }

        if (p.Sort.IsNullOrEmpty()) p.OrderBy = _.Id.Desc();

        return NodeData.Search(nodeId, start, end, p["Q"], p);
    }
}
