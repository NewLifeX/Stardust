using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Serialization;
using NewLife;
using NewLife.Data;
using NewLife.Log;
using Stardust.Models;
using XCode;

namespace Stardust.Data.Nodes;

/// <summary>节点升级通道</summary>
public enum NodeChannels
{
    /// <summary>发布</summary>
    Release = 1,

    /// <summary>测试</summary>
    Beta = 2,

    /// <summary>开发</summary>
    Develop = 3,
}

/// <summary>节点版本。发布更新</summary>
public partial class NodeVersion : Entity<NodeVersion>
{
    #region 对象操作
    static NodeVersion()
    {
        // 累加字段
        //var df = Meta.Factory.AdditionalFields;
        //df.Add(__.ProductID);

        // 过滤器 UserModule、TimeModule、IPModule
        Meta.Modules.Add<UserModule>();
        Meta.Modules.Add<TimeModule>();
        Meta.Modules.Add<IPModule>();
    }

    /// <summary>验证数据，通过抛出异常的方式提示验证失败。</summary>
    /// <param name="isNew">是否插入</param>
    public override void Valid(Boolean isNew)
    {
        // 如果没有脏数据，则不需要进行任何处理
        if (!HasDirty) return;

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

        // 默认通道
        if (Channel < NodeChannels.Release || Channel > NodeChannels.Develop) Channel = NodeChannels.Release;

        base.Valid(isNew);
    }

    /// <summary>加载后，释放规则</summary>
    protected override void OnLoad()
    {
        base.OnLoad();

        var dic = Strategy.SplitAsDictionary("=", ";");
        Rules = dic.ToDictionary(e => e.Key, e => e.Value.Split(","), StringComparer.OrdinalIgnoreCase);
    }

    protected override void InitData()
    {
        if (Meta.Count > 0) return;

        var entity = new NodeVersion
        {
            Version = "agent60-0101",
            ProductCode = "StarAgent",
            Enable = false,
            Force = true,
            Channel = NodeChannels.Release,
            Strategy = "framework=6.*;version<=2.9.2024.0101",
            Description = "星尘代理StarAgent升级策略（dotNet6.0）",
        };
        entity.Insert();

        entity = new NodeVersion
        {
            Version = "agent80-0101",
            ProductCode = "StarAgent",
            Enable = false,
            Force = true,
            Channel = NodeChannels.Release,
            Strategy = "framework=8.*;version<=2.9.2024.0101",
            Description = "星尘代理StarAgent升级策略（dotNet8.0），同时支持net6/net7的StarAgent在安装net8后升级",
        };
        entity.Insert();

        entity = new NodeVersion
        {
            Version = "CrazyCoder-0101",
            ProductCode = "CrazyCoder",
            Enable = false,
            Force = true,
            Channel = NodeChannels.Release,
            Strategy = "framework=8.*;version<=2.9.2024.0101",
            Description = "码神工具升级策略",
        };
        entity.Insert();

        entity = new NodeVersion
        {
            Version = "v8.0.1-aspnet",
            ProductCode = "dotnet",
            Enable = false,
            Force = true,
            Channel = NodeChannels.Release,
            Strategy = "framework<=7.1;oskind=centos,ubuntu,smartos;version>=2.9.2024.0101",
            Source = "/files/dotnet/",
            Description = "NET8运行时升级策略，在CentOS/Ubuntu/SmartOS等系统上，自动安装net8",
        };
        entity.Insert();

        entity = new NodeVersion
        {
            Version = "v8.0.1-host",
            ProductCode = "dotnet",
            Enable = false,
            Force = true,
            Channel = NodeChannels.Release,
            Strategy = "framework<=7.1;oskind=win20*;version>=2.9.2024.0101",
            Source = "/files/dotnet/",
            Description = "NET8运行时升级策略，在Windows服务器系统上，自动安装net8",
        };
        entity.Insert();

        entity = new NodeVersion
        {
            Version = "v8.0.1-desktop",
            ProductCode = "dotnet",
            Enable = false,
            Force = true,
            Channel = NodeChannels.Release,
            Strategy = "framework<=7.1;oskind=win1*;version>=2.9.2024.0101",
            Source = "/files/dotnet/",
            Description = "NET8运行时升级策略，在win10/win11系统上，自动安装net8",
        };
        entity.Insert();
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
    public static NodeVersion FindByID(Int32 id)
    {
        if (id <= 0) return null;

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.ID == id);

        // 单对象缓存
        return Meta.SingleCache[id];

        //return Find(_.ID == id);
    }

    /// <summary>
    /// 根据版本查找
    /// </summary>
    /// <param name="version"></param>
    /// <returns></returns>
    public static NodeVersion FindByVersion(String version)
    {
        if (version.IsNullOrEmpty()) return null;

        return Find(_.Version == version);
    }
    #endregion

    #region 高级查询

    /// <summary>
    /// 高级查询
    /// </summary>
    /// <param name="start">开始时间</param>
    /// <param name="end">结束时间</param>
    /// <param name="enable">启用</param>
    /// <param name="key">关键词</param>
    /// <param name="p">分页</param>
    /// <returns></returns>
    public static IList<NodeVersion> Search(DateTime start, DateTime end, Boolean? enable, String key, PageParameter p)
    {
        var exp = new WhereExpression();
        if (enable != null) exp &= _.Enable == enable;
        exp &= _.UpdateTime.Between(start, end);
        exp &= SearchWhereByKeys(key);

        return FindAll(exp, p);
    }

    #endregion

    #region 业务操作
    /// <summary>获取有效</summary>
    /// <param name="channel"></param>
    /// <returns></returns>
    public static IList<NodeVersion> GetValids(NodeChannels channel)
    {
        var list = Meta.Cache.FindAll(e => e.Enable);
        if (list.Count == 0) return list;

        if (channel >= NodeChannels.Release) list = list.Where(e => e.Channel == channel).ToList();

        // 按照编号降序，最大100个
        list = list.OrderByDescending(e => e.ID).Take(100).ToList();

        return list;
    }

    /// <summary>应用策略是否匹配指定节点</summary>
    /// <param name="node"></param>
    /// <returns></returns>
    public Boolean Match(Node node)
    {
        var rs = MatchResult(node);
        if (rs == null) return true;

        DefaultSpan.Current?.AppendTag($"[{ID}][{Version}] {rs}");

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
        else if (Rules.TryGetValue("runtime>", out vs))
        {
            var str = node.Runtime;
            if (str.IsNullOrEmpty()) return "Version is null";
            if (!System.Version.TryParse(str, out var ver1)) return $"Runtime=[{str}] is invalid";
            if (!System.Version.TryParse(vs[0], out var ver2)) return $"vs[0]=[{vs[0]}] is invalid";

            if (ver1 < ver2) return $"Runtime=[{ver1}] < {ver2}";
        }
        else if (Rules.TryGetValue("runtime<", out vs))
        {
            var str = node.Runtime;
            if (str.IsNullOrEmpty()) return "Version is null";
            if (!System.Version.TryParse(str, out var ver1)) return $"Runtime=[{str}] is invalid";
            if (!System.Version.TryParse(vs[0], out var ver2)) return $"vs[0]=[{vs[0]}] is invalid";

            if (ver1 > ver2) return $"Runtime=[{ver1}] > {ver2}";
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