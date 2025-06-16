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
using Stardust.Data.Nodes;
using XCode;
using XCode.Cache;
using XCode.Configuration;
using XCode.DataAccessLayer;
using XCode.Membership;
using XCode.Shards;

namespace Stardust.Data.Deployment;

public partial class AppBuildNode : Entity<AppBuildNode>
{
    #region 对象操作
    static AppBuildNode()
    {
        // 累加字段，生成 Update xx Set Count=Count+1234 Where xxx
        //var df = Meta.Factory.AdditionalFields;
        //df.Add(nameof(DeployId));

        // 过滤器 UserModule、TimeModule、IPModule
        Meta.Modules.Add(new UserModule { AllowEmpty = false });
        Meta.Modules.Add<TimeModule>();
        Meta.Modules.Add(new IPModule { AllowEmpty = false });
        Meta.Modules.Add<TraceModule>();

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

        // 处理当前已登录用户信息，可以由UserModule过滤器代劳
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

        return true;
    }

    ///// <summary>首次连接数据库时初始化数据，仅用于实体类重载，用户不应该调用该方法</summary>
    //[EditorBrowsable(EditorBrowsableState.Never)]
    //protected override void InitData()
    //{
    //    // InitData一般用于当数据表没有数据时添加一些默认数据，该实体类的任何第一次数据库操作都会触发该方法，默认异步调用
    //    if (Meta.Session.Count > 0) return;

    //    if (XTrace.Debug) XTrace.WriteLine("开始初始化AppBuildNode[编译节点]数据……");

    //    var entity = new AppBuildNode();
    //    entity.DeployName = "abc";
    //    entity.DeployId = 0;
    //    entity.NodeId = 0;
    //    entity.Enable = true;
    //    entity.SourcePath = "abc";
    //    entity.PullCode = true;
    //    entity.BuildProject = true;
    //    entity.PackageOutput = true;
    //    entity.UploadPackage = true;
    //    entity.Insert();

    //    if (XTrace.Debug) XTrace.WriteLine("完成初始化AppBuildNode[编译节点]数据！");
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
    /// <summary>节点</summary>
    [XmlIgnore, ScriptIgnore, IgnoreDataMember]
    public Node Node => Extends.Get(nameof(Node), k => Node.FindByID(NodeId));

    /// <summary>节点</summary>
    [Map(__.NodeId)]
    public String NodeName => Node?.Name;
    #endregion

    #region 高级查询
    /// <summary>高级查询</summary>
    /// <param name="deployId">应用部署集。对应AppDeploy</param>
    /// <param name="nodeId">节点。节点服务器</param>
    /// <param name="pullCode">拉取源代码</param>
    /// <param name="buildProject">编译项目</param>
    /// <param name="packageOutput">打包输出</param>
    /// <param name="uploadPackage">上传应用包</param>
    /// <param name="enable">启用</param>
    /// <param name="start">更新时间开始</param>
    /// <param name="end">更新时间结束</param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
    /// <returns>实体列表</returns>
    public static IList<AppBuildNode> Search(Int32 deployId, Int32 nodeId, Boolean? pullCode, Boolean? buildProject, Boolean? packageOutput, Boolean? uploadPackage, Boolean? enable, DateTime start, DateTime end, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (deployId >= 0) exp &= _.DeployId == deployId;
        if (nodeId >= 0) exp &= _.NodeId == nodeId;
        if (pullCode != null) exp &= _.PullCode == pullCode;
        if (buildProject != null) exp &= _.BuildProject == buildProject;
        if (packageOutput != null) exp &= _.PackageOutput == packageOutput;
        if (uploadPackage != null) exp &= _.UploadPackage == uploadPackage;
        if (enable != null) exp &= _.Enable == enable;
        exp &= _.UpdateTime.Between(start, end);
        if (!key.IsNullOrEmpty()) exp &= SearchWhereByKeys(key);

        return FindAll(exp, page);
    }

    // Select Count(Id) as Id,Category From AppBuildNode Where CreateTime>'2020-01-24 00:00:00' Group By Category Order By Id Desc limit 20
    //static readonly FieldCache<AppBuildNode> _CategoryCache = new(nameof(Category))
    //{
    //Where = _.CreateTime > DateTime.Today.AddDays(-30) & Expression.Empty
    //};

    ///// <summary>获取类别列表，字段缓存10分钟，分组统计数据最多的前20种，用于魔方前台下拉选择</summary>
    ///// <returns></returns>
    //public static IDictionary<String, String> GetCategoryList() => _CategoryCache.FindAllName();
    #endregion

    #region 业务操作
    #endregion
}
