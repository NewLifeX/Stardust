using System;
using NewLife.Caching;
using NewLife.Security;
using Stardust.Data;
using Stardust.Models;
using Stardust.Server.Services;
using Xunit;

namespace ServerTest.Services;

/// <summary>应用在线服务测试。覆盖在线记录登记、复用、查询与移除（APP-3）</summary>
public class AppOnlineServiceTests
{
    private static App CreateApp()
    {
        var app = new App { Name = "aol_" + Rand.NextString(8), Enable = true };
        app.Insert();
        return app;
    }

    [Fact(DisplayName = "首次登记同一客户端返回新记录，再次调用复用旧记录")]
    public void GetOnline_NewThenReuse()
    {
        var svc = new AppOnlineService(new CacheProvider(), null);
        var app = CreateApp();
        var clientId = "cli_" + Rand.NextString(6);
        try
        {
            var (online, isNew) = svc.GetOnline(app, clientId, null, null, "127.0.0.1");
            Assert.True(isNew);
            Assert.NotNull(online);
            Assert.Equal(app.Name, online.Name);

            var (online2, isNew2) = svc.GetOnline(app, clientId, null, null, "127.0.0.1");
            Assert.False(isNew2);
            Assert.Equal(online.Id, online2.Id);
        }
        finally
        {
            svc.RemoveOnline(clientId);
            app.Delete();
        }
    }

    [Fact(DisplayName = "移除只清缓存，数据库记录仍可按客户端标识回查")]
    public void GetOnline_ThenRemove()
    {
        var svc = new AppOnlineService(new CacheProvider(), null);
        var app = CreateApp();
        var clientId = "cli_" + Rand.NextString(10);
        try
        {
            svc.GetOnline(app, clientId, null, null, "127.0.0.1");
            Assert.NotNull(svc.GetOnline(clientId));

            // 第一次移除命中缓存
            Assert.True(svc.RemoveOnline(clientId));
            // 缓存已清空，再次移除返回 false
            Assert.False(svc.RemoveOnline(clientId));
            // 在线记录本身在数据库中，缓存清空后仍可回查（移除只影响缓存）
            Assert.NotNull(svc.GetOnline(clientId));
        }
        finally
        {
            AppOnline.FindByClient(clientId)?.Delete();
            app.Delete();
        }
    }

    [Fact(DisplayName = "查询不存在的客户端返回空")]
    public void GetOnline_UnknownClient_Null()
    {
        var svc = new AppOnlineService(new CacheProvider(), null);

        Assert.Null(svc.GetOnline("not-exists-" + Rand.NextString(8)));
    }

    [Fact(DisplayName = "更新在线信息写入应用标识、客户端标识与心跳次数")]
    public void UpdateOnline_FillsSession()
    {
        var svc = new AppOnlineService(new CacheProvider(), null);
        var app = CreateApp();
        var clientId = "cli_" + Rand.NextString(10);
        try
        {
            var info = new AppInfo { Name = app.Name, Version = "1.2.3", MachineName = "test-machine" };
            var online = svc.UpdateOnline(app, clientId, "127.0.0.1", null, info);

            Assert.NotNull(online);
            Assert.Equal(app.Id, online.AppId);
            Assert.Equal(clientId, online.Client);
            Assert.True(online.PingCount >= 1);
        }
        finally
        {
            AppOnline.FindByClient(clientId)?.Delete();
            app.Delete();
        }
    }
}
