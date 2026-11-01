using Stardust.Web.Models.Dashboard;
using Xunit;

namespace Stardust.Tests.Dashboard;

/// <summary>看板下钻链接测试。锁定列表过滤参数与详情路径，避免视图散落硬编码</summary>
public class DashboardLinksTests
{
    [Fact(DisplayName = "看板链接：项目、应用、节点详情路径正确")]
    public void Boards_BuildDetailUrls()
    {
        Assert.Equal("/Dashboard/Project/12", DashboardLinks.Project(12));
        Assert.Equal("/Dashboard/App/34", DashboardLinks.App(34));
        Assert.Equal("/Dashboard/Node/56", DashboardLinks.Node(56));
        Assert.Equal("/Dashboard/Projects", DashboardLinks.Projects());
        Assert.Equal("/Dashboard/Apps", DashboardLinks.Apps());
        Assert.Equal("/Dashboard/Nodes", DashboardLinks.Nodes());
    }

    [Fact(DisplayName = "列表链接：项目过滤参数正确")]
    public void Lists_AppendProjectFilter()
    {
        Assert.Equal("/Registry/App", DashboardLinks.AppList());
        Assert.Equal("/Registry/App?projectId=7", DashboardLinks.AppList(7));
        Assert.Equal("/Nodes/Node", DashboardLinks.NodeList());
        Assert.Equal("/Nodes/Node?projectId=7", DashboardLinks.NodeList(7));
    }

    [Fact(DisplayName = "在线实例：应用、项目、节点参数可组合")]
    public void AppOnline_CombinesFilters()
    {
        Assert.Equal("/Registry/AppOnline", DashboardLinks.AppOnline());
        Assert.Equal("/Registry/AppOnline?appId=3", DashboardLinks.AppOnline(3));
        Assert.Equal("/Registry/AppOnline?appId=3&projectId=7&nodeId=9", DashboardLinks.AppOnline(3, 7, 9));
        Assert.Equal("/Registry/AppOnline?projectId=7", DashboardLinks.AppOnline(projectId: 7));
    }

    [Fact(DisplayName = "跟踪链接：端点名称做 URL 编码")]
    public void TraceLinks_EncodeName()
    {
        Assert.Equal("/Monitors/TraceHourStat?appId=3&name=%2Fapi%2Forder", DashboardLinks.TraceHourStat(3, "/api/order"));
        Assert.Equal("/Monitors/TraceData?appId=3&name=%2Fapi%2Forder&minError=1", DashboardLinks.TraceData(3, "/api/order", true));
        Assert.Equal("/Monitors/TraceData?minError=1", DashboardLinks.TraceData(errorsOnly: true));
    }

    [Fact(DisplayName = "告警链接：活跃告警与详情路径正确")]
    public void AlarmLinks_BuildExpectedUrls()
    {
        Assert.Equal("/Monitors/AlarmRecord", DashboardLinks.AlarmList());
        Assert.Equal("/Monitors/AlarmRecord?status=1", DashboardLinks.AlarmList(true));
        Assert.Equal("/Monitors/AlarmRecord/Detail?id=123456789", DashboardLinks.AlarmDetail(123456789));
    }

    [Fact(DisplayName = "模型下钻地址：资源行与告警条目使用集中链接")]
    public void Models_UseDashboardLinks()
    {
        var region = new RegionRow { Name = "项目A", ProjectId = 7 };
        Assert.Equal("/Nodes/Node?projectId=7", region.Url);

        var alarm = new AlarmItem { Id = 123 };
        Assert.Equal("/Monitors/AlarmRecord/Detail?id=123", alarm.Url);
    }
}
