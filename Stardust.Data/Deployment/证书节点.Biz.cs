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
using Stardust.Data.Nodes;
using Stardust.Deployment;

namespace Stardust.Data.Deployment;

public partial class SslCertificateNode : Entity<SslCertificateNode>
{
    #region 对象操作
    // 控制最大缓存数量，Find/FindAll查询方法在表行数小于该值时走实体缓存
    private static Int32 MaxCacheCount = 1000;

    static SslCertificateNode()
    {
        // 累加字段，生成 Update xx Set Count=Count+1234 Where xxx
        //var df = Meta.Factory.AdditionalFields;
        //df.Add(nameof(CertId));

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
        if (NodeId > 0 && IP.IsNullOrEmpty())
        {
            var node = Node;
            if (node != null) IP = node.IP;
        }

        // 处理当前已登录用户信息，可以由UserInterceptor拦截器代劳
        /*var user = ManageProvider.User;
        if (user != null)
        {
            if (method == DataMethod.Insert && !Dirtys[nameof(CreateUserId)]) CreateUserId = user.ID;
            if (!Dirtys[nameof(UpdateUserId)]) UpdateUserId = user.ID;
        }*/
        //if (method == DataMethod.Insert && !Dirtys[nameof(CreateTime)]) CreateTime = DateTime.Now;
        //if (!Dirtys[nameof(UpdateTime)]) UpdateTime = DateTime.Now;
        //if (method == DataMethod.Insert && !Dirtys[nameof(CreateIP)]) CreateIP = ManageProvider.UserHost;
        //if (!Dirtys[nameof(UpdateIP)]) UpdateIP = ManageProvider.UserHost;

        // 检查唯一索引
        // CheckExist(method == DataMethod.Insert, nameof(CertId), nameof(NodeId));

        return true;
    }

    ///// <summary>首次连接数据库时初始化数据，仅用于实体类重载，用户不应该调用该方法</summary>
    //[EditorBrowsable(EditorBrowsableState.Never)]
    //protected override void InitData()
    //{
    //    // InitData一般用于当数据表没有数据时添加一些默认数据，该实体类的任何第一次数据库操作都会触发该方法，默认异步调用
    //    if (Meta.Session.Count > 0) return;

    //    if (XTrace.Debug) XTrace.WriteLine("开始初始化SslCertificateNode[证书节点]数据……");

    //    var entity = new SslCertificateNode();
    //    entity.CertId = 0;
    //    entity.NodeId = 0;
    //    entity.IP = "abc";
    //    entity.Enable = true;
    //    entity.TargetPath = "abc";
    //    entity.Version = "abc";
    //    entity.LastPush = DateTime.Now;
    //    entity.Status = "abc";
    //    entity.Insert();

    //    if (XTrace.Debug) XTrace.WriteLine("完成初始化SslCertificateNode[证书节点]数据！");
    //}

    /// <summary>已重载。插入后修正证书的关联节点数</summary>
    /// <returns></returns>
    protected override Int32 OnInsert()
    {
        var rs = base.OnInsert();

        Cert?.Fix();

        return rs;
    }

    /// <summary>已重载。删除后修正证书的关联节点数</summary>
    /// <returns></returns>
    protected override Int32 OnDelete()
    {
        var rs = base.OnDelete();

        Cert?.Fix();

        return rs;
    }
    #endregion

    #region 扩展属性
    #endregion

    #region 高级查询

    // Select Count(Id) as Id,Category From SslCertificateNode Where CreateTime>'2020-01-24 00:00:00' Group By Category Order By Id Desc limit 20
    //static readonly FieldCache<SslCertificateNode> _CategoryCache = new(nameof(Category))
    //{
    //Where = _.CreateTime > DateTime.Today.AddDays(-30) & Expression.Empty
    //};

    ///// <summary>获取类别列表，字段缓存10分钟，分组统计数据最多的前20种，用于魔方前台下拉选择</summary>
    ///// <returns></returns>
    //public static IDictionary<String, String> GetCategoryList() => _CategoryCache.FindAllName();
    #endregion

    #region 业务操作
    /// <summary>下发目录。节点配置优先，其次证书配置，最后使用 ../certs</summary>
    /// <returns></returns>
    public String GetTargetPath()
    {
        if (!TargetPath.IsNullOrEmpty()) return TargetPath.Trim();

        var cert = Cert;
        return cert != null ? cert.GetTargetPath() : CertManager.DefaultCertPath;
    }

    /// <summary>修正节点上的证书信息，补齐 IP 与启用状态</summary>
    /// <returns></returns>
    public Int32 Fix()
    {
        var node = Node;
        if (node != null)
        {
            IP = node.IP;
            if (!Enable && node.Enable) Enable = true;
        }

        return Update();
    }

    #endregion
}
