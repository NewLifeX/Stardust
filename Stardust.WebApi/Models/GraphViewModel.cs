namespace Stardust.Web.Models;

/// <summary>调用链关系图视图模型。与 MVC 真源 Stardust.Web/Models/GraphViewModel.cs 同构，供 GET /api/trace/graph 返回节点边数据</summary>
public class GraphViewModel
{
    public String TraceId { get; set; }

    public String Title { get; set; }

    public String Layout { get; set; }

    public GraphCategory[] Categories { get; set; }

    public GraphLink[] Links { get; set; }

    public GraphNode[] Nodes { get; set; }
}

/// <summary>关系图分类</summary>
public class GraphCategory
{
    public String Name { get; set; }

    public String Symbol { get; set; }
}

/// <summary>关系图调用边</summary>
public class GraphLink
{
    public String Source { get; set; }

    public String Target { get; set; }
}

/// <summary>关系图节点</summary>
public class GraphNode
{
    public String Id { get; set; }

    public String Name { get; set; }

    public Int32 Category { get; set; }

    public Double SymbolSize { get; set; }

    public Double Value { get; set; }
}
