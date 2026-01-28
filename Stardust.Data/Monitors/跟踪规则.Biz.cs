using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.RegularExpressions;
using NewLife;
using NewLife.Log;
using XCode;
using XCode.Membership;

namespace Stardust.Data.Monitors;

public partial class TraceRule : Entity<TraceRule>
{
    #region 对象操作
    static TraceRule()
    {
        // 累加字段，生成 Update xx Set Count=Count+1234 Where xxx
        //var df = Meta.Factory.AdditionalFields;
        //df.Add(nameof(UpdateUserID));

        // 过滤器 UserInterceptor、TimeInterceptor、IPInterceptor
        Meta.Interceptors.Add<UserInterceptor>();
        Meta.Interceptors.Add<TimeInterceptor>();
        Meta.Interceptors.Add<IPInterceptor>();
    }

    /// <summary>验证并修补数据，通过抛出异常的方式提示验证失败。</summary>
    /// <param name="isNew">是否插入</param>
    public override void Valid(Boolean isNew)
    {
        // 如果没有脏数据，则不需要进行任何处理
        if (!HasDirty) return;

        // 建议先调用基类方法，基类方法会做一些统一处理
        base.Valid(isNew);

        // 在新插入数据或者修改了指定字段时进行修正
        // 处理当前已登录用户信息，可以由UserModule过滤器代劳
        /*var user = ManageProvider.User;
        if (user != null)
        {
            if (!Dirtys[nameof(UpdateUserID)]) UpdateUserID = user.ID;
        }*/
        //if (isNew && !Dirtys[nameof(CreateTime)]) CreateTime = DateTime.Now;
        //if (!Dirtys[nameof(UpdateTime)]) UpdateTime = DateTime.Now;
        //if (isNew && !Dirtys[nameof(CreateIP)]) CreateIP = ManageProvider.UserHost;
        //if (!Dirtys[nameof(UpdateIP)]) UpdateIP = ManageProvider.UserHost;
    }

    /// <summary>首次连接数据库时初始化数据，仅用于实体类重载，用户不应该调用该方法</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    protected override void InitData()
    {
        // InitData一般用于当数据表没有数据时添加一些默认数据，该实体类的任何第一次数据库操作都会触发该方法，默认异步调用
        if (Meta.Session.Count > 0) return;

        if (XTrace.Debug) XTrace.WriteLine("开始初始化TraceRule[跟踪规则]数据……");

        var entity = new TraceRule
        {
            Rule = "^/Admin/[A-Z]\\w+",
            Enable = true,
            IsWhite = true,
            IsRegex = true,
        };
        entity.Insert();

        entity = new TraceRule
        {
            Rule = "^/Cube/[A-Z]\\w+",
            Enable = true,
            IsWhite = true,
            IsRegex = true,
        };
        entity.Insert();

        entity = new TraceRule
        {
            Rule = "/Sso/*",
            Enable = true,
            IsWhite = true,
        };
        entity.Insert();

        if (XTrace.Debug) XTrace.WriteLine("完成初始化TraceRule[跟踪规则]数据！");
    }
    #endregion

    #region 扩展属性
    #endregion

    #region 扩展查询
    /// <summary>根据编号查找</summary>
    /// <param name="id">编号</param>
    /// <returns>实体对象</returns>
    public static TraceRule FindById(Int32 id)
    {
        if (id <= 0) return null;

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.Id == id);

        // 单对象缓存
        return Meta.SingleCache[id];

        //return Find(_.Id == id);
    }
    #endregion

    #region 高级查询

    // Select Count(Id) as Id,Category From TraceRule Where CreateTime>'2020-01-24 00:00:00' Group By Category Order By Id Desc limit 20
    //static readonly FieldCache<TraceRule> _CategoryCache = new FieldCache<TraceRule>(nameof(Category))
    //{
    //Where = _.CreateTime > DateTime.Today.AddDays(-30) & Expression.Empty
    //};

    ///// <summary>获取类别列表，字段缓存10分钟，分组统计数据最多的前20种，用于魔方前台下拉选择</summary>
    ///// <returns></returns>
    //public static IDictionary<String, String> GetCategoryList() => _CategoryCache.FindAllName();
    #endregion

    #region 业务操作
    /// <summary>获取有效规则</summary>
    /// <returns></returns>
    public static IList<TraceRule> GetValids() => FindAllWithCache().Where(e => e.Enable && !e.Rule.IsNullOrEmpty()).OrderByDescending(e => e.Priority).ToList();

    Regex _regex;
    /// <summary>是否匹配输入</summary>
    /// <param name="input"></param>
    /// <returns></returns>
    public Boolean IsMatch(String input)
    {
        if (input.IsNullOrWhiteSpace()) return false;
        if (Rule.IsNullOrEmpty()) return false;

        if (IsRegex)
        {
            _regex ??= new Regex(Rule, RegexOptions.Compiled);
            return _regex.IsMatch(input);
        }
        else
        {
            if (Rule.EqualIgnoreCase(input)) return true;
            if (Rule.IsMatch(input, StringComparison.OrdinalIgnoreCase)) return true;
        }

        return false;
    }

    /// <summary>尝试使用所有规则匹配目标输入</summary>
    /// <param name="input">目标输入</param>
    /// <returns></returns>
    public static TraceRule Match(String input)
    {
        if (input.IsNullOrWhiteSpace()) return null;

        // 后面有正则判断，不能转换大小写
        //input = input.Trim().ToLower();
        input = input.Trim();

        var rules = GetValids();
        foreach (var rule in rules)
        {
            if (rule.IsMatch(input)) return rule;
        }

        return null;
    }
    #endregion
}