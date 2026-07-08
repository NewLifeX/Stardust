using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.Serialization;
using System.Xml.Serialization;
using NewLife;
using NewLife.Data;
using NewLife.Log;
using NewLife.Serialization;
using XCode;
using XCode.Membership;

namespace Stardust.Data.Gateway;

/// <summary>网关路由。请求匹配规则，定义如何将请求转发到目标集群</summary>
public partial class GatewayRoute : Entity<GatewayRoute>
{
    #region 对象操作
    static GatewayRoute()
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
    }
    #endregion

    #region 扩展属性
    /// <summary>解析请求头匹配规则为字典</summary>
    [XmlIgnore, IgnoreDataMember]
    public IDictionary<String, String> HeaderRules
    {
        get
        {
            if (Headers.IsNullOrEmpty()) return null;
            return JsonParser.Decode(Headers) as IDictionary<String, String>;
        }
    }

    /// <summary>解析添加请求头规则为字典</summary>
    [XmlIgnore, IgnoreDataMember]
    public IDictionary<String, String> AddHeaderRules
    {
        get
        {
            if (AddHeaders.IsNullOrEmpty()) return null;
            return JsonParser.Decode(AddHeaders) as IDictionary<String, String>;
        }
    }

    /// <summary>是否允许 WebSocket 升级。当路由未显式设置时，默认允许</summary>
    [XmlIgnore, IgnoreDataMember]
    public Boolean WebSocketEnabled => WebSocket;

    /// <summary>域名列表</summary>
    [XmlIgnore, IgnoreDataMember]
    public String[] DomainList => (Domain + "").Split(',', StringSplitOptions.RemoveEmptyEntries);

    /// <summary>路径列表</summary>
    [XmlIgnore, IgnoreDataMember]
    public String[] PathList => (Path + "").Split(',', StringSplitOptions.RemoveEmptyEntries);

    /// <summary>方法列表</summary>
    [XmlIgnore, IgnoreDataMember]
    public String[] MethodList => (Methods + "").Split(',', StringSplitOptions.RemoveEmptyEntries);
    #endregion

    #region 扩展查询
    /// <summary>获取所有启用的路由，按优先级降序排列</summary>
    public static IList<GatewayRoute> FindAllEnabled()
    {
        return FindAll(_.Enable == true, _.Priority.Desc(), null, 0, 0);
    }

    /// <summary>搜索路由</summary>
    public static IList<GatewayRoute> Search(String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (!key.IsNullOrEmpty()) exp &= _.Name.Contains(key) | _.Domain.Contains(key) | _.Path.Contains(key) | _.Remark.Contains(key);

        return FindAll(exp, page);
    }
    #endregion

    #region 匹配逻辑
    /// <summary>判断请求是否匹配此路由</summary>
    /// <param name="domain">请求域名</param>
    /// <param name="path">请求路径</param>
    /// <param name="method">HTTP方法</param>
    /// <param name="headers">请求头字典（用于Header匹配）</param>
    /// <returns></returns>
    public Boolean Match(String domain, String path, String method, IDictionary<String, String> headers = null)
    {
        // 检查域名
        if (!Domain.IsNullOrEmpty())
        {
            if (!MatchDomain(domain)) return false;
        }

        // 检查路径
        if (!Path.IsNullOrEmpty())
        {
            if (!MatchPath(path)) return false;
        }

        // 检查方法
        if (!Methods.IsNullOrEmpty())
        {
            if (!MatchMethod(method)) return false;
        }

        // 检查请求头
        if (!Headers.IsNullOrEmpty())
        {
            if (!MatchHeaders(headers)) return false;
        }

        return true;
    }

    private Boolean MatchDomain(String domain)
    {
        var domains = DomainList;
        if (domains == null || domains.Length == 0) return true;

        foreach (var item in domains)
        {
            var d = item.Trim();
            if (d.IsNullOrEmpty()) continue;

            // 精确匹配
            if (d.EqualIgnoreCase(domain)) return true;

            // 通配符匹配 *.example.com
            if (d.StartsWith("*."))
            {
                var suffix = d.TrimStart("*.");
                if (domain.EndsWith("." + suffix, StringComparison.OrdinalIgnoreCase)) return true;
            }

            // 通配符 *
            if (d == "*") return true;
        }

        return false;
    }

    private Boolean MatchPath(String path)
    {
        var paths = PathList;
        if (paths == null || paths.Length == 0) return true;

        foreach (var item in paths)
        {
            var p = item.Trim();
            if (p.IsNullOrEmpty()) continue;

            // 前缀匹配 /api/*
            if (p.EndsWith("/*"))
            {
                var prefix = p.TrimEnd("/*");
                if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return true;
                continue;
            }

            // 精确匹配
            if (p.EqualIgnoreCase(path)) return true;
        }

        return false;
    }

    private Boolean MatchMethod(String method)
    {
        var methods = MethodList;
        if (methods == null || methods.Length == 0) return true;

        return methods.Any(e => e.Trim().EqualIgnoreCase(method));
    }

    /// <summary>匹配请求头</summary>
    /// <param name="headers">请求头字典</param>
    /// <returns></returns>
    public Boolean MatchHeaders(IDictionary<String, String> headers)
    {
        if (Headers.IsNullOrEmpty()) return true;

        var rules = HeaderRules;
        if (rules == null || rules.Count == 0) return true;

        if (headers == null) return false;

        foreach (var rule in rules)
        {
            var key = rule.Key;
            var value = rule.Value;

            // 检查请求头是否存在
            if (!headers.TryGetValue(key, out var hv)) return false;

            // 通配符匹配
            if (value == "*") continue;

            // 前缀匹配
            if (value.EndsWith("*"))
            {
                var prefix = value.TrimEnd('*');
                if (!hv.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return false;
            }
            // 后缀匹配
            else if (value.StartsWith("*"))
            {
                var suffix = value.TrimStart('*');
                if (!hv.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)) return false;
            }
            // 精确匹配或包含
            else if (!hv.EqualIgnoreCase(value) && !hv.Contains(value))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>获取去除了前缀的路径</summary>
    public String StripPathPrefix(String path)
    {
        if (!StripPrefix || Path.IsNullOrEmpty()) return path;

        var paths = PathList;
        if (paths == null || paths.Length == 0) return path;

        foreach (var item in paths)
        {
            var p = item.Trim();
            if (p.IsNullOrEmpty()) continue;

            if (p.EndsWith("/*"))
            {
                var prefix = p.TrimEnd("/*");
                if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    return "/" + path.Substring(prefix.Length).TrimStart('/');
            }
        }

        return path;
    }
    #endregion
}
