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
    /// <remarks>
    /// 先注册 StarApi 路由，再调用基类注册内置 API 和静态文件。
    /// 基类注册的 /* 通配符会放在 Dictionary 末尾，因此通配匹配时
    /// /star/* 会优先于 /* 被检查到，避免被静态文件处理器拦截。
    /// </remarks>
    protected override void RegisterRoutes()
    {
        // 1. 先注册 StarApi 控制器（通配 /star/*），确保排在 /* 前面
        Server.MapController<StarApi>("/star");

        // 2. 再调用基类注册内置 API（/api/*）+ 静态文件（/*）
        base.RegisterRoutes();
    }
    #endregion

    #region 扩展面板
    // 保留基类 GetExtensions()，子类可重写以添加自定义扩展面板
    #endregion
}
#endif
