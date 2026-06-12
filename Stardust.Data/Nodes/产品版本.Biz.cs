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

namespace Stardust.Data.Nodes;

public partial class ProductRelease : Entity<ProductRelease>
{
    #region 对象操作
    // 控制最大缓存数量，Find/FindAll查询方法在表行数小于该值时走实体缓存
    private static Int32 MaxCacheCount = 1000;

    static ProductRelease()
    {
        // 累加字段，生成 Update xx Set Count=Count+1234 Where xxx
        //var df = Meta.Factory.AdditionalFields;
        //df.Add(nameof(Channel));

        // 拦截器 UserInterceptor、TimeInterceptor、IPInterceptor
        Meta.Interceptors.Add(new UserInterceptor { AllowEmpty = false });
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
        //if (method == DataMethod.Delete) return true;
        // 如果没有脏数据，则不需要进行任何处理
        if (!HasDirty) return true;

        // 建议先调用基类方法，基类方法会做一些统一处理
        if (!base.Valid(method)) return false;

        // 在新插入数据或者修改了指定字段时进行修正

        // 处理当前已登录用户信息，可以由UserInterceptor拦截器代劳
        /*var user = ManageProvider.User;
        if (user != null)
        {
            if (method == DataMethod.Insert && !Dirtys[nameof(CreateUserID)]) CreateUserID = user.ID;
            if (!Dirtys[nameof(UpdateUserID)]) UpdateUserID = user.ID;
        }*/
        //if (method == DataMethod.Insert && !Dirtys[nameof(CreateTime)]) CreateTime = DateTime.Now;
        //if (!Dirtys[nameof(UpdateTime)]) UpdateTime = DateTime.Now;
        //if (method == DataMethod.Insert && !Dirtys[nameof(CreateIP)]) CreateIP = ManageProvider.UserHost;
        //if (!Dirtys[nameof(UpdateIP)]) UpdateIP = ManageProvider.UserHost;

        // 检查唯一索引
        // CheckExist(method == DataMethod.Insert, nameof(Version));

        return true;
    }

    ///// <summary>首次连接数据库时初始化数据，仅用于实体类重载，用户不应该调用该方法</summary>
    //[EditorBrowsable(EditorBrowsableState.Never)]
    //protected override void InitData()
    //{
    //    // InitData一般用于当数据表没有数据时添加一些默认数据，该实体类的任何第一次数据库操作都会触发该方法，默认异步调用
    //    if (Meta.Session.Count > 0) return;

    //    if (XTrace.Debug) XTrace.WriteLine("开始初始化ProductRelease[产品版本]数据……");

    //    var entity = new ProductRelease();
    //    entity.Version = "abc";
    //    entity.ProductCode = "abc";
    //    entity.Enable = true;
    //    entity.Force = true;
    //    entity.Channel = 0;
    //    entity.Insert();

    //    if (XTrace.Debug) XTrace.WriteLine("完成初始化ProductRelease[产品版本]数据！");
    //}

    ///// <summary>已重载。基类先调用Valid(true)验证数据，然后在事务保护内调用OnInsert</summary>
    ///// <returns></returns>
    //public override Int32 Insert()
    //{
    //    return base.Insert();
    //}

    ///// <summary>已重载。在事务保护范围内处理业务，位于Valid之后</summary>
    ///// <returns></returns>
    //protected override Int32 OnDelete()
    //{
    //    return base.OnDelete();
    //}
    #endregion

    #region 扩展属性
    #endregion

    #region 高级查询

    // Select Count(Id) as Id,ProductCode From ProductRelease Where CreateTime>'2020-01-24 00:00:00' Group By ProductCode Order By Id Desc limit 20
    static readonly FieldCache<ProductRelease> _ProductCodeCache = new(nameof(ProductCode))
    {
        //Where = _.CreateTime > DateTime.Today.AddDays(-30) & Expression.Empty
    };

    /// <summary>获取产品编码列表，字段缓存10分钟，分组统计数据最多的前20种，用于魔方前台下拉选择</summary>
    /// <returns></returns>
    public static IDictionary<String, String> GetProductCodeList() => _ProductCodeCache.FindAllName();
    #endregion

    #region 业务操作
    /// <summary>获取有效版本列表</summary>
    /// <param name="channel">升级通道</param>
    /// <returns></returns>
    public static IList<ProductRelease> GetValids(NodeChannels channel)
    {
        var list = Meta.Cache.FindAll(e => e.Enable);
        if (list.Count == 0) return list;

        if (channel >= NodeChannels.Release) list = list.Where(e => e.Channel == channel).ToList();

        // 按照编号降序，最大100个
        list = list.OrderByDescending(e => e.Id).Take(100).ToList();

        return list;
    }

    /// <summary>匹配节点，找到当前Release中适合指定节点的Package</summary>
    /// <param name="node">节点</param>
    /// <returns>匹配的Package，若无匹配返回null</returns>
    public ProductPackage MatchPackage(Node node)
    {
        if (!Enable) return null;

        var packages = ProductPackage.FindAllByReleaseId(Id);
        if (packages == null || packages.Count == 0) return null;

        // 提取节点运行时主版本号
        var runtimeVer = node.Runtime;
        var runtimeMajor = "";
        if (!runtimeVer.IsNullOrEmpty())
        {
            var p = runtimeVer.IndexOf('.');
            runtimeMajor = p > 0 ? runtimeVer[..p] : runtimeVer;
        }

        // 第一步：精确匹配 TargetRuntime
        if (!runtimeMajor.IsNullOrEmpty())
        {
            var pkg = packages.FirstOrDefault(e => e.TargetRuntime == runtimeMajor);
            if (pkg != null) return pkg;
        }

        // 第二步：net45 特殊处理（Runtime为空时尝试匹配"4"）
        if (runtimeMajor.IsNullOrEmpty() || runtimeMajor == "4")
        {
            var pkg = packages.FirstOrDefault(e => e.TargetRuntime == "4");
            if (pkg != null) return pkg;
        }

        // 第三步：匹配万能包（TargetRuntime="*"的跨版本升级包）
        var fallback = packages.FirstOrDefault(e => e.TargetRuntime == "*");
        if (fallback != null) return fallback;

        return null;
    }
    #endregion
}
