using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Serialization;
using NewLife;
using NewLife.Data;
using NewLife.Log;
using Stardust.Data.Nodes;
using Stardust.Models;
using XCode;

namespace Stardust.Data.Deployment;

/// <summary>部署版本</summary>
public partial class AppDeployVersion : Entity<AppDeployVersion>
{
    #region 对象操作
    static AppDeployVersion()
    {
        // 累加字段，生成 Update xx Set Count=Count+1234 Where xxx
        //var df = Meta.Factory.AdditionalFields;
        //df.Add(nameof(DeployId));

        // 过滤器 UserModule、TimeModule、IPModule
        Meta.Modules.Add<UserModule>();
        Meta.Modules.Add<TimeModule>();
        Meta.Modules.Add<IPModule>();
        Meta.Modules.Add<TraceModule>();
    }

    /// <summary>验证并修补数据，通过抛出异常的方式提示验证失败。</summary>
    /// <param name="isNew">是否插入</param>
    public override void Valid(Boolean isNew)
    {
        // 如果没有脏数据，则不需要进行任何处理
        if (!HasDirty) return;

        // 这里验证参数范围，建议抛出参数异常，指定参数名，前端用户界面可以捕获参数异常并聚焦到对应的参数输入框
        if (Version.IsNullOrEmpty()) throw new ArgumentNullException(nameof(Version), "版本不能为空！");

        // 重新聚合规则
        if (!Dirtys[__.Strategy] && Rules != null)
        {
            var sb = new StringBuilder();
            foreach (var item in Rules)
            {
                if (sb.Length > 0) sb.Append(";");
                sb.AppendFormat("{0}={1}", item.Key, item.Value.Join(","));
            }
            Strategy = sb.ToString();
        }

        // 建议先调用基类方法，基类方法会做一些统一处理
        base.Valid(isNew);

        // 在新插入数据或者修改了指定字段时进行修正
        // 处理当前已登录用户信息，可以由UserModule过滤器代劳
        /*var user = ManageProvider.User;
        if (user != null)
        {
            if (isNew && !Dirtys[nameof(CreateUserId)]) CreateUserId = user.ID;
            if (!Dirtys[nameof(UpdateUserId)]) UpdateUserId = user.ID;
        }*/
        //if (isNew && !Dirtys[nameof(CreateTime)]) CreateTime = DateTime.Now;
        //if (!Dirtys[nameof(UpdateTime)]) UpdateTime = DateTime.Now;
        //if (isNew && !Dirtys[nameof(CreateIP)]) CreateIP = ManageProvider.UserHost;
        //if (!Dirtys[nameof(UpdateIP)]) UpdateIP = ManageProvider.UserHost;

        // 检查唯一索引
        // CheckExist(isNew, nameof(DeployId), nameof(Version));
    }

    /// <summary>加载后，释放规则</summary>
    protected override void OnLoad()
    {
        base.OnLoad();

        var dic = Strategy.SplitAsDictionary("=", ";");
        Rules = dic.ToDictionary(e => e.Key, e => e.Value.Split(","), StringComparer.OrdinalIgnoreCase);
    }
    #endregion

    #region 扩展属性
    /// <summary>规则集合</summary>
    [XmlIgnore]
    public IDictionary<String, String[]> Rules { get; set; }
    #endregion

    #region 扩展查询
    /// <summary>根据编号查找</summary>
    /// <param name="id">编号</param>
    /// <returns>实体对象</returns>
    public static AppDeployVersion FindById(Int32 id)
    {
        if (id <= 0) return null;

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.Id == id);

        // 单对象缓存
        return Meta.SingleCache[id];

        //return Find(_.Id == id);
    }

    /// <summary>根据应用、版本查找</summary>
    /// <param name="deployId">应用</param>
    /// <param name="version">版本</param>
    /// <returns>实体对象</returns>
    public static AppDeployVersion FindByDeployIdAndVersion(Int32 deployId, String version)
    {
        //// 实体缓存
        //if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.DeployId == deployId && e.Version.EqualIgnoreCase(version));

        return Find(_.DeployId == deployId & _.Version == version);
    }

    /// <summary>根据应用查找</summary>
    /// <param name="deployId">应用</param>
    /// <param name="count">个数</param>
    /// <returns>实体列表</returns>
    public static IList<AppDeployVersion> FindAllByDeployId(Int32 deployId, Int32 count = 20)
    {
        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.DeployId == deployId).OrderByDescending(e => e.Id).Take(count).ToList();

        return FindAll(_.DeployId == deployId & _.Enable == true, _.Id.Desc(), null, 0, count);
    }
    #endregion

    #region 高级查询
    /// <summary>高级查询</summary>
    /// <param name="deployId">应用</param>
    /// <param name="version">版本</param>
    /// <param name="enable">启用</param>
    /// <param name="start">更新时间开始</param>
    /// <param name="end">更新时间结束</param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
    /// <returns>实体列表</returns>
    public static IList<AppDeployVersion> Search(Int32 deployId, String version, Boolean? enable, DateTime start, DateTime end, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (deployId >= 0) exp &= _.DeployId == deployId;
        if (!version.IsNullOrEmpty()) exp &= _.Version == version;
        if (enable != null) exp &= _.Enable == enable;
        exp &= _.UpdateTime.Between(start, end);
        if (!key.IsNullOrEmpty()) exp &= _.Url.Contains(key) | _.CreateIP.Contains(key) | _.UpdateIP.Contains(key) | _.Remark.Contains(key);

        return FindAll(exp, page);
    }
    #endregion

    #region 业务操作
    /// <summary>应用策略是否匹配指定节点</summary>
    /// <param name="node"></param>
    /// <returns></returns>
    public Boolean Match(Node node)
    {
        var rs = MatchResult(node);
        if (rs == null) return true;

        DefaultSpan.Current?.AppendTag($"[{Id}][{Version}] {rs}");

        return false;
    }

    /// <summary>应用策略是否匹配指定节点</summary>
    /// <param name="node"></param>
    /// <returns></returns>
    public String MatchResult(Node node)
    {
        // 没有使用该规则，直接过
        if (Rules.TryGetValue("version", out var vs))
        {
            var ver = node.Version;
            if (ver.IsNullOrEmpty() || !vs.Any(e => e.IsMatch(ver, StringComparison.OrdinalIgnoreCase)))
                return $"[{ver}] not Match {vs.Join(",")}";
        }
        else if (Rules.TryGetValue("version>", out vs))
        {
            var ver = node.Version;
            if (node.Version.IsNullOrEmpty()) return "Version is null";
            if (!System.Version.TryParse(ver, out var ver1)) return $"Version=[{ver}] is invalid";
            if (!System.Version.TryParse(vs[0], out var ver2)) return $"vs[0]=[{vs[0]}] is invalid";

            if (ver1 < ver2) return $"Version=[{ver1}] < {ver2}";
        }
        else if (Rules.TryGetValue("version<", out vs))
        {
            var ver = node.Version;
            if (node.Version.IsNullOrEmpty()) return "Version is null";
            if (!System.Version.TryParse(ver, out var ver1)) return $"Version=[{ver}] is invalid";
            if (!System.Version.TryParse(vs[0], out var ver2)) return $"vs[0]=[{vs[0]}] is invalid";

            if (ver1 > ver2) return $"Version=[{ver1}] > {ver2}";
        }

        if (Rules.TryGetValue("node", out vs))
        {
            var code = node.Code;
            var name = node.Name;
            if (code.IsNullOrEmpty() && name.IsNullOrEmpty()) return "Node is null";
            if ((code.IsNullOrEmpty() || !vs.Any(e => e.IsMatch(code, StringComparison.OrdinalIgnoreCase))) &&
                (name.IsNullOrEmpty() || !vs.Any(e => e.IsMatch(name, StringComparison.OrdinalIgnoreCase))))
                return $"[{code}/{name}] not Match {vs.Join(",")}";
        }

        if (Rules.TryGetValue("category", out vs))
        {
            var category = node.Category;
            if (category.IsNullOrEmpty() || !vs.Any(e => e.IsMatch(category, StringComparison.OrdinalIgnoreCase)))
                return $"[{category}] not Match {vs.Join(",")}";
        }

        if (Rules.TryGetValue("runtime", out vs))
        {
            var runtime = node.Runtime;
            if (runtime.IsNullOrEmpty() || !vs.Any(e => e.IsMatch(runtime))) return $"[{runtime}] not Match {vs.Join(",")}";
        }

        if (Rules.TryGetValue("framework", out vs))
        {
            var str = !node.Frameworks.IsNullOrEmpty() ? node.Frameworks : node.Framework;
            var frameworks = str?.Split(",");
            if (frameworks == null || frameworks.Length == 0) return "Frameworks is null";

            // 本节点拥有的所有框架，任意框架匹配任意规则，即可认为匹配
            var flag = false;
            foreach (var item in frameworks)
            {
                if (vs.Any(e => e.IsMatch(item)))
                {
                    flag = true;
                    break;
                }
            }
            if (!flag) return $"[{str}] not Match {vs.Join(",")}";
        }
        else if (Rules.TryGetValue("framework>", out vs))
        {
            var str = node.Framework;
            if (str.IsNullOrEmpty()) return "Version is null";
            if (!System.Version.TryParse(str, out var ver1)) return $"Framework=[{str}] is invalid";
            if (!System.Version.TryParse(vs[0], out var ver2)) return $"vs[0]=[{vs[0]}] is invalid";

            if (ver1 < ver2) return $"Framework=[{ver1}] < {ver2}";
        }
        else if (Rules.TryGetValue("framework<", out vs))
        {
            var str = node.Framework;
            if (str.IsNullOrEmpty()) return "Version is null";
            if (!System.Version.TryParse(str, out var ver1)) return $"Framework=[{str}] is invalid";
            if (!System.Version.TryParse(vs[0], out var ver2)) return $"vs[0]=[{vs[0]}] is invalid";

            if (ver1 > ver2) return $"Framework=[{ver1}] > {ver2}";
        }

        if (Rules.TryGetValue("os", out vs))
        {
            var os = node.OS;
            if (os.IsNullOrEmpty() || !vs.Any(e => e.IsMatch(os, StringComparison.OrdinalIgnoreCase)))
                return $"[{os}] not Match {vs.Join(",")}";
        }

        if (Rules.TryGetValue("oskind", out vs))
        {
            var os = node.OSKind;
            if (os <= 0) return "OSKind is null";

            var flag = false;
            foreach (var item in vs)
            {
                if (item.ToInt() == (Int32)os)
                {
                    flag = true;
                    break;
                }
                if (Enum.TryParse<OSKinds>(item, true, out var v) && v == os)
                {
                    flag = true;
                    break;
                }
            }
            if (!flag) return $"[{os}] not Match {vs.Join(",")}";
        }

        if (Rules.TryGetValue("arch", out vs))
        {
            var arch = node.Architecture;
            if (arch.IsNullOrEmpty() || !vs.Any(e => e.IsMatch(arch, StringComparison.OrdinalIgnoreCase)))
                return $"[{arch}] not Match {vs.Join(",")}";
        }

        if (Rules.TryGetValue("province", out vs))
        {
            var code = node.ProvinceID + "";
            var name = node.ProvinceName;
            if (code.IsNullOrEmpty() && name.IsNullOrEmpty()) return "Province is null";
            if ((code.IsNullOrEmpty() || !vs.Any(e => e.IsMatch(code, StringComparison.OrdinalIgnoreCase))) &&
                (name.IsNullOrEmpty() || !vs.Any(e => e.IsMatch(name, StringComparison.OrdinalIgnoreCase))))
                return $"[{code}/{name}] not Match {vs.Join(",")}";
        }

        if (Rules.TryGetValue("city", out vs))
        {
            var code = node.CityID + "";
            var name = node.CityName;
            if (code.IsNullOrEmpty() && name.IsNullOrEmpty()) return "City is null";
            if ((code.IsNullOrEmpty() || !vs.Any(e => e.IsMatch(code, StringComparison.OrdinalIgnoreCase))) &&
                (name.IsNullOrEmpty() || !vs.Any(e => e.IsMatch(name, StringComparison.OrdinalIgnoreCase))))
                return $"[{code}/{name}] not Match {vs.Join(",")}";
        }

        //if (Rules.TryGetValue("product", out vs))
        //{
        //    var product = node.ProductCode;
        //    if (product.IsNullOrEmpty() || !vs.Any(e => e.IsMatch(product))) return false;
        //}

        return null;
    }
    #endregion
}