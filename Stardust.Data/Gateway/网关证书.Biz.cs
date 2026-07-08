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

/// <summary>网关证书。SSL证书配置，用于HTTPS终止和SNI匹配</summary>
/// <remarks>已废弃：请使用 Stardust.Data.Deployment.SslCertificate 统一管理证书。</remarks>
public partial class GatewayCert : Entity<GatewayCert>
{
    #region 对象操作
    static GatewayCert()
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

        if (Name.IsNullOrEmpty()) throw new ArgumentNullException(nameof(Name), "名称不能为空！");
        if (Domain.IsNullOrEmpty()) throw new ArgumentNullException(nameof(Domain), "域名不能为空！");
    }
    #endregion

    #region 扩展查询
    /// <summary>获取所有启用的证书</summary>
    public static IList<GatewayCert> FindAllEnabled() => FindAll(_.Enable == true);

    /// <summary>搜索证书</summary>
    public static IList<GatewayCert> Search(String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (!key.IsNullOrEmpty()) exp &= _.Name.Contains(key) | _.Domain.Contains(key) | _.Remark.Contains(key);

        return FindAll(exp, page);
    }
    #endregion
}
