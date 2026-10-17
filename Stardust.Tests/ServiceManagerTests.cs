using System;
using System.Linq;
using NewLife;
using Stardust.Managers;
using Stardust.Models;
using Xunit;

namespace Stardust.Tests;

/// <summary>应用服务管理测试。覆盖服务集合的增删、配置拷贝与按进程查询，不启动真实进程</summary>
public class ServiceManagerTests
{
    [Fact(DisplayName = "添加服务后可查询，同名服务覆盖且不计新增")]
    public void Add_And_Replace()
    {
        using var manager = new ServiceManager();

        Assert.Empty(manager.Services ?? []);
        Assert.Empty(manager.RunningServices);

        var count = manager.Add(
            new ServiceInfo { Name = "app1", FileName = "a.dll" },
            new ServiceInfo { Name = "app2", FileName = "b.dll" });
        Assert.Equal(2, count);
        Assert.Equal(2, manager.Services!.Length);

        // 名称不区分大小写，同名覆盖不增加数量
        var added = manager.Add(new ServiceInfo { Name = "APP1", FileName = "c.dll" });
        Assert.Equal(0, added);
        Assert.Equal(2, manager.Services!.Length);
        Assert.Equal("c.dll", manager.Services!.First(e => e.Name.EqualIgnoreCase("app1")).FileName);

        // 名称为空的服务被忽略
        manager.Add(new ServiceInfo { Name = "", FileName = "d.dll" });
        Assert.Equal(2, manager.Services!.Length);
    }

    [Fact(DisplayName = "移除服务返回删除数量，不存在时返回0")]
    public void Remove_Works()
    {
        using var manager = new ServiceManager();
        manager.Add(new ServiceInfo { Name = "app1" }, new ServiceInfo { Name = "app2" });

        Assert.Equal(1, manager.Remove("app1"));
        Assert.Single(manager.Services!);
        Assert.Equal(0, manager.Remove("app1"));
        Assert.Equal(0, manager.Remove(""));
    }

    [Fact(DisplayName = "SetServices 克隆配置，外部对象修改不影响内部")]
    public void SetServices_Clones()
    {
        using var manager = new ServiceManager();
        var services = new[] { new ServiceInfo { Name = "app1", FileName = "a.dll" } };

        manager.SetServices(services);
        services[0].FileName = "changed.dll";

        Assert.Single(manager.Services!);
        Assert.Equal("a.dll", manager.Services![0].FileName);
    }

    [Fact(DisplayName = "按进程编号查询控制器，非法编号或未托管进程返回空")]
    public void QueryByProcess_ReturnsNull()
    {
        using var manager = new ServiceManager();

        Assert.Null(manager.QueryByProcess(0));
        Assert.Null(manager.QueryByProcess(-1));
        Assert.Null(manager.QueryByProcess(Environment.ProcessId));
    }
}
