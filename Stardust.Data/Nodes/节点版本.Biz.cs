using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Serialization;
using NewLife;
using NewLife.Data;
using Stardust.Models;
using XCode;
using XCode.Membership;

namespace Stardust.Data.Nodes
{
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
            // 比较产品类型
            if (!ProductCode.IsNullOrEmpty() && !ProductCode.EqualIgnoreCase(node.ProductCode)) return false;

            // 没有使用该规则，直接过
            if (Rules.TryGetValue("version", out var vs))
            {
                var ver = node.Version;
                if (ver.IsNullOrEmpty() || !vs.Any(e => e.IsMatch(ver))) return false;
            }
            else if (Rules.TryGetValue("version>", out vs))
            {
                if (node.Version.IsNullOrEmpty()) return false;
                if (!System.Version.TryParse(node.Version, out var ver1)) return false;
                if (!System.Version.TryParse(vs[0], out var ver2)) return false;

                if (ver1 < ver2) return false;
            }
            else if (Rules.TryGetValue("version<", out vs))
            {
                if (node.Version.IsNullOrEmpty()) return false;
                if (!System.Version.TryParse(node.Version, out var ver1)) return false;
                if (!System.Version.TryParse(vs[0], out var ver2)) return false;

                if (ver1 > ver2) return false;
            }

            if (Rules.TryGetValue("node", out vs))
            {
                var code = node.Code;
                var name = node.Name;
                if ((code.IsNullOrEmpty() || !vs.Any(e => e.IsMatch(code))) &&
                    (name.IsNullOrEmpty() || !vs.Any(e => e.IsMatch(name)))) return false;
            }

            if (Rules.TryGetValue("category", out vs))
            {
                var category = node.Category;
                if (category.IsNullOrEmpty() || !vs.Any(e => e.IsMatch(category))) return false;
            }

            if (Rules.TryGetValue("runtime", out vs))
            {
                var runtime = node.Runtime;
                if (runtime.IsNullOrEmpty() || !vs.Any(e => e.IsMatch(runtime))) return false;
            }

            if (Rules.TryGetValue("framework", out vs))
            {
                var str = !node.Frameworks.IsNullOrEmpty() ? node.Frameworks : node.Framework;
                var frameworks = str?.Split(",");
                if (frameworks == null || frameworks.Length == 0) return false;

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
                if (!flag) return false;
            }
            else if (Rules.TryGetValue("framework>", out vs))
            {
                if (node.Framework.IsNullOrEmpty()) return false;
                if (!System.Version.TryParse(node.Framework, out var ver1)) return false;
                if (!System.Version.TryParse(vs[0], out var ver2)) return false;

                if (ver1 < ver2) return false;
            }
            else if (Rules.TryGetValue("framework<", out vs))
            {
                if (node.Framework.IsNullOrEmpty()) return false;
                if (!System.Version.TryParse(node.Framework, out var ver1)) return false;
                if (!System.Version.TryParse(vs[0], out var ver2)) return false;

                if (ver1 > ver2) return false;
            }

            if (Rules.TryGetValue("os", out vs))
            {
                var os = node.OS;
                if (os.IsNullOrEmpty() || !vs.Any(e => e.IsMatch(os))) return false;
            }

            if (Rules.TryGetValue("oskind", out vs))
            {
                var os = node.OSKind;
                //if (os <= 0 || !vs.Any(e => e.ToInt() == (Int32)os || Enum.TryParse<OSKinds>(e, out var v) && v == os)) return false;
                if (os <= 0) return false;

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
                if (!flag) return false;
            }

            if (Rules.TryGetValue("arch", out vs))
            {
                var arch = node.Architecture;
                if (arch.IsNullOrEmpty() || !vs.Any(e => e.EqualIgnoreCase(arch))) return false;
            }

            if (Rules.TryGetValue("province", out vs))
            {
                var province = node.ProvinceID + "";
                if (province.IsNullOrEmpty() || !vs.Any(e => e.IsMatch(province))) return false;
            }

            if (Rules.TryGetValue("city", out vs))
            {
                var city = node.CityID + "";
                if (city.IsNullOrEmpty() || !vs.Any(e => e.IsMatch(city))) return false;
            }

            //if (Rules.TryGetValue("product", out vs))
            //{
            //    var product = node.ProductCode;
            //    if (product.IsNullOrEmpty() || !vs.Any(e => e.IsMatch(product))) return false;
            //}

            return true;
        }
        #endregion
    }
}