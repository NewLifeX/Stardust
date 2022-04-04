namespace Stardust.Web.Models
{
    public class GraphViewModel
    {
        public String Title { get; set; }

        public GraphCategory[] Categories { get; set; }

        public GraphLink[] Links { get; set; }

        public GraphNode[] Nodes { get; set; }
    }

    public class GraphCategory
    {
        public String Name { get; set; }
    }

    public class GraphLink
    {
        public String Source { get; set; }

        public String Target { get; set; }
    }

    public class GraphNode
    {
        public String Id { get; set; }

        public String Name { get; set; }

        public Double SymbolSize { get; set; }

        public Double X { get; set; }

        public Double Y { get; set; }

        public Double Value { get; set; }

        public Int32 Category { get; set; }
    }
}