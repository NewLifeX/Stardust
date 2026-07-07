using System;
using System.Collections.Generic;
using System.Linq;
using NewLife;
using NewLife.Data;
using XCode;
using XCode.Membership;

namespace Stardust.Data.Gateway;

/// <summary>网关集群。后端服务器集群，定义了负载均衡策略和健康检查配置</summary>
public partial class GatewayCluster : Entity<GatewayCluster>
{
    #region 对象操作
    static GatewayCluster()
    {
        Meta.Interceptors.Add<UserInterceptor>();
        Meta.Interceptors.Add<TimeInterceptor>();
    }

    public override void Valid(Boolean isNew)
    {
        if (!HasDirty) return;
        base.Valid(isNew);
        if (Name.IsNullOrEmpty()) throw new ArgumentNullException(nameof(Name), "名称不能为空！");
    }
    #endregion

    #region 扩展属性
    public IList<GatewayNode> Nodes => Extends.Get(nameof(Nodes), k => GatewayNode.FindAllByClusterId(Id));
    public IList<GatewayRoute> Routes => Extends.Get(nameof(Routes), k => GatewayRoute.FindAllByClusterId(Id));
    public Int32 ActiveNodes => Nodes?.Count(e => e.Enable && e.IsHealthy) ?? 0;
    #endregion

    #region 扩展查询
    public static IList<GatewayCluster> FindAllEnabled() => FindAll(_.Enable == true);

    public static IList<GatewayCluster> Search(String key, PageParameter page)
    {
        var exp = new WhereExpression();
        if (!key.IsNullOrEmpty()) exp &= _.Name.Contains(key) | _.Remark.Contains(key);
        return FindAll(exp, page);
    }
    #endregion

    #region 扩展删除
    protected override Int32 OnDelete()
    {
        GatewayNode.DeleteByClusterId(Id);
        return base.OnDelete();
    }
    #endregion
}
