using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Script.Serialization;
using System.Xml.Serialization;
using NewLife;
using NewLife.Data;
using NewLife.Log;
using NewLife.Model;
using NewLife.Reflection;
using NewLife.Threading;
using NewLife.Web;
using XCode;
using XCode.Cache;
using XCode.Configuration;
using XCode.DataAccessLayer;
using XCode.Membership;
using XCode.Shards;

namespace Stardust.Data.Platform;

public partial class McpServer : Entity<McpServer>
{
    #region 对象操作
    // 控制最大缓存数量，Find/FindAll查询方法在表行数小于该值时走实体缓存
    private static Int32 MaxCacheCount = 1000;

    static McpServer()
    {
        // 拦截器 UserInterceptor、TimeInterceptor、IPInterceptor
        Meta.Interceptors.Add<TimeInterceptor>();
        Meta.Interceptors.Add(new IPInterceptor { AllowEmpty = false });

        // 实体缓存
        // var ec = Meta.Cache;
        // ec.Expire = 60;
    }

    /// <summary>验证并修补数据，返回验证结果，或者通过抛出异常的方式提示验证失败。</summary>
    /// <param name="method">添删改方法</param>
    public override Boolean Valid(DataMethod method)
    {
        // 如果没有脏数据，则不需要进行任何处理
        if (!HasDirty) return true;

        // 这里验证参数范围，建议抛出参数异常，指定参数名
        if (Name.IsNullOrEmpty()) throw new ArgumentNullException(nameof(Name), L.T("Name cannot be empty."));

        // 建议先调用基类方法，基类方法会做一些统一处理
        if (!base.Valid(method)) return false;

        return true;
    }
    #endregion

    #region 扩展属性
    #endregion

    #region 高级查询

    /// <summary>高级查询</summary>
    /// <param name="enable">启用</param>
    /// <param name="projectId">项目编号。所属项目GalaxyProject.Id，0表示未归属</param>
    /// <param name="start">更新时间开始</param>
    /// <param name="end">更新时间结束</param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
    /// <returns>实体列表</returns>
    public static IList<McpServer> Search(Boolean? enable, Int32 projectId, DateTime start, DateTime end, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (enable != null) exp &= _.Enable == enable;
        if (projectId > 0) exp &= _.ProjectId == projectId;
        exp &= _.UpdateTime.Between(start, end);
        if (!key.IsNullOrEmpty()) exp &= SearchWhereByKeys(key);

        return FindAll(exp, page);
    }
    #endregion

    #region 业务操作
    #endregion
}
