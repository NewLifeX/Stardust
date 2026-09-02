extern alias web;
using System.ComponentModel;
using NewLife.Cube.Widgets;
using Xunit;

namespace ServerTest.Web;

/// <summary>StarWeb 工作台 KPI 组件测试。验证业务组件被魔方工作台自动发现且元数据完整（无需数据库）</summary>
public class WidgetTests
{
    private readonly WidgetManager _manager = new();

    /// <summary>星尘业务 KPI 组件清单（WidgetAttribute.Name）</summary>
    private static readonly String[] Names = ["OnlineNode", "NodeCount", "OnlineApp", "TodayCall", "ErrorToday", "Alarming"];

    /// <summary>确保 StarWeb 程序集已加载，WidgetManager.Scan 才会扫描到其中的业务组件</summary>
    static WidgetTests() => _ = typeof(web::Stardust.Web.Widgets.KpiData);

    [Fact]
    [DisplayName("扫描发现全部星尘业务组件且元数据完整")]
    public void Scan_FindsStarWidgets()
    {
        var dic = _manager.Scan();

        // 星尘业务组件应全部存在
        foreach (var name in Names)
            Assert.True(dic.ContainsKey(name), $"未找到部件 {name}");

        var info = dic["OnlineNode"];
        Assert.Equal("在线节点", info.Title);
        Assert.Equal("星尘平台", info.Category);
        Assert.Equal(WidgetTypes.Kpi, info.WidgetType);
        Assert.True(info.Cols <= 3, "KPI 卡宽度应不大于3");
        Assert.False(info.AdminOnly, "运维总览卡应所有登录用户可见");
        Assert.NotNull(info.Type);
        Assert.True(typeof(IWidget).IsAssignableFrom(info.Type));
        Assert.True(info.Type.IsPublic);
        Assert.Equal("Stardust.Web.Widgets", info.Type.Namespace);
    }

    [Fact]
    [DisplayName("星尘 KPI 组件全部为小卡、统一分类，普通登录用户可见")]
    public void KpiWidgets_MetadataValid()
    {
        var dic = _manager.Scan();

        foreach (var name in Names)
        {
            var info = dic[name];
            Assert.True(info.Cols <= 3, $"KPI 部件 {name} 宽度应不大于3");
            Assert.Equal(WidgetTypes.Kpi, info.WidgetType);
            Assert.Equal("星尘平台", info.Category);
            Assert.False(info.AdminOnly, $"KPI 部件 {name} 应所有登录用户可见");
        }
    }

    [Fact]
    [DisplayName("KPI 数据契约为公开 KpiData 类（Value/Trend/Url），而非匿名类型")]
    public void KpiData_IsPublicContract()
    {
        var kpiType = typeof(web::Stardust.Web.Widgets.KpiData);

        // 公开类型 + 公开属性，供跨程序集动态绑定渲染
        Assert.True(kpiType.IsPublic);
        Assert.Equal("String", kpiType.GetProperty("Value")?.PropertyType.Name);
        Assert.Equal("String", kpiType.GetProperty("Trend")?.PropertyType.Name);
        Assert.Equal("String", kpiType.GetProperty("Url")?.PropertyType.Name);

        // 各组件声明实现 IWidget 契约（GetData 返回 Object，运行时返回 KpiData 实例）
        var dic = _manager.Scan();
        foreach (var name in Names)
        {
            var info = dic[name];
            Assert.NotNull(info.Type);
            Assert.True(typeof(IWidget).IsAssignableFrom(info.Type), $"部件 {name} 应实现 IWidget");
            Assert.NotNull(info.Type.GetMethod("GetData"));
        }
    }
}
