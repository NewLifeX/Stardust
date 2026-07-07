using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.Serialization;
using System.Xml.Serialization;
using NewLife;
using NewLife.Data;
using NewLife.Log;
using XCode;
using XCode.Membership;

namespace Stardust.Data.Gateway;

/// <summary>网关节点。集群中的后端服务器节点</summary>
public partial class GatewayNode : Entity<GatewayNode>
{
    #region 对象操作
    static GatewayNode()
    {
        Meta.Interceptors.Add<UserInterceptor>();
        Meta.Interceptors.Add<TimeInterceptor>();
    }

    /// <summary>验证并修补数据</summary>
    /// <param name="isNew">是否插入</param>
    public override void Valid(Boolean isNew)
    {
        if (!HasDirty) return;

        base.Valid(isNew);

        if (Address.IsNullOrEmpty()) throw new ArgumentNullException(nameof(Address), "地址不能为空！");

        if (Weight <= 0) Weight = 1;
        if (MaxFails <= 0) MaxFails = 3;
        if (FailTimeout <= 0) FailTimeout = 60;
    }
    #endregion

    #region 扩展查询
    /// <summary>获取集群中所有启用的健康节点</summary>
    public static IList<GatewayNode> FindAllHealthyByCluster(Int32 clusterId)
    {
        if (clusterId <= 0) return [];

        return FindAll(_.ClusterId == clusterId & _.Enable == true & _.IsHealthy == true);
    }

    /// <summary>搜索节点</summary>
    public static IList<GatewayNode> Search(Int32 clusterId, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (clusterId > 0) exp &= _.ClusterId == clusterId;
        if (!key.IsNullOrEmpty()) exp &= _.Name.Contains(key) | _.Address.Contains(key) | _.Remark.Contains(key);

        return FindAll(exp, page);
    }
    #endregion

    #region 扩展删除
    /// <summary>根据集群删除所有节点</summary>
    public static Int32 DeleteByClusterId(Int32 clusterId)
    {
        if (clusterId <= 0) return 0;

        return Delete(_.ClusterId == clusterId);
    }
    #endregion
}
