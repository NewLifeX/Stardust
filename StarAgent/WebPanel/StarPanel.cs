#if !NET40
using NewLife;
using NewLife.Agent;
using NewLife.Agent.WebPanel;
using NewLife.Http;
using NewLife.Log;

namespace StarAgent.WebPanel;

/// <summary>StarAgent Web管理面板</summary>
/// <remarks>
/// 继承 AgentWebPanel，定制 StarAgent 专有功能：
/// - 重写嵌入资源路径以使用自己的静态文件
/// - 注册自定义 API 控制器（子服务管理、星尘配置、本机信息）
/// - 注册扩展面板
/// </remarks>
public class StarPanel : AgentWebPanel
{
    #region 属性
    /// <summary>嵌入式静态文件的资源命名空间前缀</summary>
    protected override String EmbeddedResourcePrefix => "StarAgent.WebPanel.wwwroot";

    /// <summary>嵌入式静态文件所在程序集的标记类型</summary>
    protected override Type EmbeddedResourceAssemblyType => typeof(StarPanel);

    /// <summary>所属服务</summary>
    public new ServiceBase Service { get; }
    #endregion

    #region 构造
    /// <summary>实例化StarAgent Web管理面板</summary>
    /// <param name="service">所属服务</param>
    public StarPanel(ServiceBase service) : base(service)
    {
        Service = service;
    }
    #endregion

    #region 路由注册
    /// <summary>注册所有路由</summary>
    protected override void RegisterRoutes()
    {
        // 先注册基类路由（内置 ApiController + 静态文件）
        base.RegisterRoutes();

        // 注册 StarAgent 专有 API 控制器，路由前缀 /api/star
        Server.MapController<StarApi>("/api/star");
    }
    #endregion

    #region 扩展面板
    // 保留基类 GetExtensions()，子类可重写以添加自定义扩展面板
    #endregion
}
#endif
