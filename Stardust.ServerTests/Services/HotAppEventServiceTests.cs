using NewLife.Security;
using Stardust.Data;
using Stardust.Server.Services;
using Xunit;

namespace ServerTest.Services;

/// <summary>热窗口事件发布者校验测试。验证仅平台管理端应用可发布热门应用通知，防止任意应用刷热窗口放大写量</summary>
public class HotAppEventServiceTests
{
    [Fact(DisplayName = "空标识与未知标识被拒绝")]
    public void IsAllowedPublisher_Invalid()
    {
        Assert.False(HotAppEventService.IsAllowedPublisher(null));
        Assert.False(HotAppEventService.IsAllowedPublisher(""));
        Assert.False(HotAppEventService.IsAllowedPublisher("10.99.99.99@999999"));
    }

    [Fact(DisplayName = "平台管理端应用在线时放行，普通应用拒绝")]
    public void IsAllowedPublisher_Whitelist()
    {
        // 普通应用：不在白名单
        var normal = new App { Name = "sva_" + Rand.NextString(8), Enable = true, Secret = Rand.NextString(16) };
        normal.Insert();
        var normalClient = $"10.88.88.89@{Rand.Next(10000, 99999)}";
        var normalOnline = new AppOnline { AppId = normal.Id, Name = normal.Name, Client = normalClient, CreateTime = DateTime.Now, UpdateTime = DateTime.Now };
        normalOnline.Insert();

        // 平台管理端应用：可能已存在，则复用
        var web = App.FindByName("StarWeb");
        var created = false;
        if (web == null)
        {
            web = new App { Name = "StarWeb", Enable = true, Secret = Rand.NextString(16) };
            web.Insert();
            created = true;
        }
        var webClient = $"10.88.88.88@{Rand.Next(10000, 99999)}";
        var webOnline = new AppOnline { AppId = web.Id, Name = web.Name, Client = webClient, CreateTime = DateTime.Now, UpdateTime = DateTime.Now };
        webOnline.Insert();

        // 插入后清理实体缓存，保证 FindByClient 立即可见
        AppOnline.Meta.Cache.Clear(nameof(HotAppEventServiceTests));

        try
        {
            Assert.True(HotAppEventService.IsAllowedPublisher(webClient));
            Assert.False(HotAppEventService.IsAllowedPublisher(normalClient));
        }
        finally
        {
            webOnline.Delete();
            normalOnline.Delete();
            normal.Delete();
            if (created) web.Delete();
        }
    }
}
