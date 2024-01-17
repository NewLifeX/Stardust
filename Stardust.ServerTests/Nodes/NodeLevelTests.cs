using System;
using System.Collections.Generic;
using Stardust.Data.Nodes;
using Stardust.Server.Services;
using Xunit;

namespace Stardust.ServerTests.Nodes;

public class NodeLevelTests
{
    //[Fact]
    //public void NodeCodeLevel()
    //{
    //    var rs = new SortedList<Int32, Node>
    //    {
    //        { 1, new Node { ID = 1, Code = "1" } },
    //        { 2, new Node { ID = 2, Code = "2" } },
    //        { 2, new Node { ID = 3, Code = "3" } },
    //        { 4, new Node { ID = 4, Code = "4" } }
    //    };

    //    Assert.Equal(4, rs.Count);
    //    Assert.Equal(4, rs.Values.Count);
    //}

    //[Fact]
    //public void NodeCodeLevel2()
    //{
    //    var rs = new SortedDictionary<Int32, Node>();
    //    rs.Add(1, new Node { ID = 1, Code = "1" });
    //    rs.Add(2, new Node { ID = 2, Code = "2" });
    //    rs.Add(2, new Node { ID = 3, Code = "3" });
    //    rs.Add(4, new Node { ID = 4, Code = "4" });

    //    Assert.Equal(4, rs.Count);
    //    Assert.Equal(4, rs.Values.Count);
    //}

    [Fact]
    public void NodeCodeLevel3()
    {
        var rs = new SortedList<Int32, IList<Node>>();
        {
            var list = new List<Node>();
            list.Add(new Node { ID = 1, Code = "1" });
            rs.Add(1, list);
        }
        {
            var list = new List<Node>();
            list.Add(new Node { ID = 2, Code = "2" });
            rs.Add(2, list);

            list.Add(new Node { ID = 3, Code = "3" });
        }
        //{
        //    var list = new List<Node>();
        //    list.Add(new Node { ID = 3, Code = "3" });
        //    rs.Add(2, list);
        //}
        {
            var list = new List<Node>();
            list.Add(new Node { ID = 4, Code = "4" });
            rs.Add(4, list);
        }

        Assert.Equal(3, rs.Count);
        Assert.Equal(3, rs.Values.Count);

        var node = rs.Values[0][0];
        Assert.Equal(1, node.ID);
    }

    [Fact]
    public void BinarySearch()
    {
        var list = new List<Int32>();
        list.Add(1);
        list.Add(2);
        list.Add(3);
        list.Add(4);
        list.Add(5);
        list.Add(7);

        var rs = list.BinarySearch(0);
        Assert.Equal(-1, rs);

        rs = list.BinarySearch(1);
        Assert.Equal(0, rs);

        rs = list.BinarySearch(2);
        Assert.Equal(1, rs);

        rs = list.BinarySearch(3);
        Assert.Equal(2, rs);

        rs = list.BinarySearch(4);
        Assert.Equal(3, rs);

        rs = list.BinarySearch(5);
        Assert.Equal(4, rs);

        rs = list.BinarySearch(6);
        Assert.Equal(-6, rs);
        rs = ~rs;
        Assert.Equal(5, rs);

        rs = list.BinarySearch(7);
        Assert.Equal(5, rs);

        rs = list.BinarySearch(8);
        Assert.Equal(-7, rs);
        rs = ~rs;
        Assert.Equal(6, rs);
    }

    [Fact]
    public void MySortedList()
    {
        var rs = new MySortedList<Int32, Node>();
        rs.Add(4, new Node { ID = 4, Code = "4" });
        rs.Add(2, new Node { ID = 2, Code = "2" });
        rs.Add(1, new Node { ID = 1, Code = "1" });
        rs.Add(2, new Node { ID = 3, Code = "3" });

        Assert.Equal(4, rs.Keys.Count);
        Assert.Equal(4, rs.Values.Count);

        var node = rs.Values[0];
        Assert.Equal(1, node.ID);

        node = rs.Values[1];
        Assert.Equal(2, node.ID);

        node = rs.Values[2];
        Assert.Equal(3, node.ID);

        node = rs.Values[3];
        Assert.Equal(4, node.ID);
    }
}
