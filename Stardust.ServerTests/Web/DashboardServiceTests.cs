extern alias web;

using System;
using System.ComponentModel;
using Stardust.Data.Monitors;
using Xunit;
using web::Stardust.Web.Models.Dashboard;
using web::Stardust.Web.Services;

namespace ServerTest.Web;

/// <summary>看板数据服务测试</summary>
/// <remarks>
/// 站点根路径直接呈现系统级看板，其报警与事件查询按雪花主键（时间片）取最近数据。
/// 历史版本按没有索引的 StartTime/CreateTime 排序，数据量大时首页会超时，本用例锁定该行为。
/// </remarks>
public class DashboardServiceTests
{
    private static String NewName() => "dash-" + Guid.NewGuid().ToString("n")[..8];

    [Fact(DisplayName = "系统看板取到最近报警并计入实时事件")]
    public void BuildSystem_ContainsRecentAlarm()
    {
        var category = NewName();
        var record = new AlarmRecord
        {
            Name = category,
            Category = category,
            Action = NewName(),
            Status = AlarmStatuses.Alarming,
            StartTime = DateTime.Now,
        };

        try
        {
            record.Insert();

            var scope = new ScopeInfo
            {
                IsAdmin = true,
                Mode = "full",
                TotalProjects = 1,
                VisibleProjects = 1,
                ProjectIds = [1],
                Projects = [new ProjectOption { Id = 1, Name = "测试项目" }],
            };

            var model = new DashboardService().BuildSystem(scope, "24h");

            Assert.NotNull(model);
            Assert.Contains(model.Events, e => e.AlarmId == record.Id);
        }
        finally
        {
            record.Delete();
        }
    }

    [Fact(DisplayName = "无可见项目时看板返回空模型而不访问数据库")]
    public void BuildSystem_NoneScope_ReturnsEmpty()
    {
        var scope = new ScopeInfo { Mode = "none" };

        var model = new DashboardService().BuildSystem(scope, "24h");

        Assert.NotNull(model);
        Assert.Empty(model.Events);
        Assert.Empty(model.Kpis);
    }
}
