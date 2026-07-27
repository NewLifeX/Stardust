using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using System.Text.RegularExpressions;
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

public partial class McpAudit : Entity<McpAudit>
{
    #region 对象操作
    // 控制最大缓存数量，Find/FindAll查询方法在表行数小于该值时走实体缓存
    private static Int32 MaxCacheCount = 1000;

    static McpAudit()
    {
        // 累加字段，生成 Update xx Set Count=Count+1234 Where xxx
        //var df = Meta.Factory.AdditionalFields;
        //df.Add(nameof(TokenId));

        // 拦截器 UserInterceptor、TimeInterceptor、IPInterceptor
        Meta.Interceptors.Add<TimeInterceptor>();
        Meta.Interceptors.Add<TraceInterceptor>();

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
        //if (method == DataMethod.Insert && !Dirtys[nameof(CreateTime)]) CreateTime = DateTime.Now;

        return true;
    }

    ///// <summary>首次连接数据库时初始化数据，仅用于实体类重载，用户不应该调用该方法</summary>
    //[EditorBrowsable(EditorBrowsableState.Never)]
    //protected override void InitData()
    //{
    //    // InitData一般用于当数据表没有数据时添加一些默认数据，该实体类的任何第一次数据库操作都会触发该方法，默认异步调用
    //    if (Meta.Session.Count > 0) return;

    //    if (XTrace.Debug) XTrace.WriteLine("开始初始化McpAudit[MCP审计日志]数据……");

    //    var entity = new McpAudit();
    //    entity.TokenId = 0;
    //    entity.TokenName = "abc";
    //    entity.ToolName = "abc";
    //    entity.ActionName = "abc";
    //    entity.CallerIp = "abc";
    //    entity.CallerUserAgent = "abc";
    //    entity.Arguments = "abc";
    //    entity.Success = true;
    //    entity.ErrorMessage = "abc";
    //    entity.Duration = 0;
    //    entity.Insert();

    //    if (XTrace.Debug) XTrace.WriteLine("完成初始化McpAudit[MCP审计日志]数据！");
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

    // Select Count(Id) as Id,ToolName From McpAudit Where CreateTime>'2020-01-24 00:00:00' Group By ToolName Order By Id Desc limit 20
    static readonly FieldCache<McpAudit> _ToolNameCache = new(nameof(ToolName))
    {
        //Where = _.CreateTime > DateTime.Today.AddDays(-30) & Expression.Empty
    };

    /// <summary>获取工具名列表，字段缓存10分钟，分组统计数据最多的前20种，用于魔方前台下拉选择</summary>
    /// <returns></returns>
    public static IDictionary<String, String> GetToolNameList() => _ToolNameCache.FindAllName();

    // Select Count(Id) as Id,ActionName From McpAudit Where CreateTime>'2020-01-24 00:00:00' Group By ActionName Order By Id Desc limit 20
    static readonly FieldCache<McpAudit> _ActionNameCache = new(nameof(ActionName))
    {
        //Where = _.CreateTime > DateTime.Today.AddDays(-30) & Expression.Empty
    };

    /// <summary>获取动作名列表，字段缓存10分钟，分组统计数据最多的前20种，用于魔方前台下拉选择</summary>
    /// <returns></returns>
    public static IDictionary<String, String> GetActionNameList() => _ActionNameCache.FindAllName();
    #endregion

    #region 业务操作

    /// <summary>按TokenId查审计日志</summary>
    /// <param name="tokenId">令牌ID</param>
    /// <param name="success">成功状态过滤</param>
    /// <param name="start">开始时间</param>
    /// <param name="end">结束时间</param>
    /// <param name="key">关键字（匹配ActionName/ToolName/ErrorMessage）</param>
    /// <param name="page">分页参数</param>
    public static IList<McpAudit> Search(Int32 tokenId, Boolean? success, DateTime start, DateTime end, String key, PageParameter page)
    {
        var exp = new WhereExpression();
        if (tokenId > 0) exp &= _.TokenId == tokenId;
        if (success.HasValue) exp &= _.Success == success.Value;
        exp &= _.CreateTime.Between(start, end);
        if (!key.IsNullOrEmpty()) exp &= (_.ActionName.Contains(key) | _.ToolName.Contains(key) | _.ErrorMessage.Contains(key));
        return FindAll(exp, page);
    }

    /// <summary>写入审计日志（异步，不阻塞主调用）</summary>
    public static void WriteAsync(Int32 tokenId, String tokenName, String toolName, String actionName,
        String ip, String ua, String arguments, Boolean success, String error, Int32 duration, String traceId)
    {
        Task.Run(() =>
        {
            var audit = new McpAudit
            {
                TokenId = tokenId,
                TokenName = tokenName,
                ToolName = toolName,
                ActionName = actionName,
                CallerIp = ip,
                CallerUserAgent = ua,
                Arguments = SanitizeArguments(arguments),
                Success = success,
                ErrorMessage = error?.Cut(500),
                Duration = duration,
                TraceId = traceId,
                CreateTime = DateTime.Now,
            };
            audit.Insert();
        });
    }

    /// <summary>脱敏敏感字段（password/secret/token/authorization），并截断2000字符</summary>
    private static String SanitizeArguments(String json)
    {
        if (json.IsNullOrEmpty()) return json;
        // 截断 2000 字符
        if (json.Length > 2000) json = json.Substring(0, 2000) + "...";
        // 脱敏 password/secret/token/authorization 字段
        var sanitized = Regex.Replace(json,
            @"(?i)(""(?:password|secret|token|authorization)""\s*:\s*"")[^""]*("")",
            "$1***$2");
        return sanitized;
    }
    #endregion
}
