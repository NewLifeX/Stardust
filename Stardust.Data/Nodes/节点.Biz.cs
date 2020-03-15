using NewLife.Data;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using XCode;
using XCode.Membership;

namespace Stardust.Data.Nodes
{
    /// <summary>节点信息</summary>
    public partial class Node : EntityBase<Node>
    {
        #region 对象操作
        static Node()
        {
            var df = Meta.Factory.AdditionalFields;
            df.Add(__.Logins);
            //!!! OnlineTime是新加字段，允许空，导致累加操作失败，暂时关闭累加
            //df.Add(__.OnlineTime);

            Meta.Modules.Add<UserModule>();
            Meta.Modules.Add<TimeModule>();
            Meta.Modules.Add<IPModule>();

            var sc = Meta.SingleCache;
            sc.Expire = 30 * 60;
            sc.FindSlaveKeyMethod = e => Find(__.Code, e);
            sc.GetSlaveKeyMethod = e => e.Code;
            //sc.SlaveKeyIgnoreCase = false;
        }

        /// <summary>验证数据，通过抛出异常的方式提示验证失败。</summary>
        /// <param name="isNew"></param>
        public override void Valid(Boolean isNew)
        {
            // 如果没有脏数据，则不需要进行任何处理
            if (!HasDirty) return;

            if (Name.IsNullOrEmpty()) throw new ArgumentNullException(__.Name, _.Name.DisplayName + "不能为空！");

            var len = _.MACs.Length;
            if (MACs != null && len > 0 && MACs.Length > len) MACs = MACs.Substring(0, len);
            len = _.COMs.Length;
            if (COMs != null && len > 0 && COMs.Length > len) COMs = COMs.Substring(0, len);
        }

        /// <summary>已重载</summary>
        /// <returns></returns>
        public override String ToString() => Code;
        #endregion

        #region 扩展属性
        /// <summary>省份</summary>
        public Area Province => Extends.Get(nameof(Province), k => Area.FindByID(ProvinceID));

        /// <summary>省份名</summary>
        [Map(__.ProvinceID)]
        public String ProvinceName => Province + "";

        /// <summary>城市</summary>
        public Area City => Extends.Get(nameof(City), k => Area.FindByID(CityID));

        /// <summary>城市名</summary>
        [Map(__.CityID)]
        public String CityName => City + "";

        /// <summary>最后地址。IP=>Address</summary>
        [DisplayName("最后地址")]
        public String LastLoginAddress => LastLoginIP.IPToAddress();
        #endregion

        #region 扩展查询
        /// <summary>根据编号查找</summary>
        /// <param name="id">编号</param>
        /// <returns></returns>
        public static Node FindByID(Int32 id)
        {
            if (id <= 0) return null;

            if (Meta.Count < 1000) return Meta.Cache.Entities.FirstOrDefault(e => e.ID == id);

            // 单对象缓存
            return Meta.SingleCache[id];
        }

        /// <summary>根据名称。登录用户名查找</summary>
        /// <param name="name">名称。登录用户名</param>
        /// <returns></returns>
        public static Node FindByName(String name)
        {
            if (name.IsNullOrEmpty()) return null;

            if (Meta.Count < 1000) return Meta.Cache.Entities.FirstOrDefault(e => e.Name == name);

            return Find(__.Name, name);
        }

        /// <summary>根据Mac</summary>
        /// <param name="mac">Mac</param>
        /// <returns></returns>
        public static Node FindByMac(String mac)
        {
            if (mac.IsNullOrEmpty()) return null;

            return Find(_.MACs.Contains(mac));
        }

        /// <summary>根据名称查找</summary>
        /// <param name="code">名称</param>
        /// <param name="cache">是否走缓存</param>
        /// <returns>实体对象</returns>
        public static Node FindByCode(String code, Boolean cache = true)
        {
            if (code.IsNullOrEmpty()) return null;

            if (!cache) return Find(_.Code == code);

            // 实体缓存
            if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.Code == code);

            // 单对象缓存
            return Meta.SingleCache.GetItemWithSlaveKey(code) as Node;
        }
        #endregion

        #region 高级查询
        /// <summary>根据唯一标识搜索，任意一个匹配即可</summary>
        /// <param name="uuid"></param>
        /// <param name="guid"></param>
        /// <param name="macs"></param>
        /// <returns></returns>
        public static IList<Node> Search(String uuid, String guid, String macs)
        {
            var exp = new WhereExpression();
            if (!uuid.IsNullOrEmpty()) exp &= _.Uuid == uuid;
            if (!guid.IsNullOrEmpty()) exp &= _.MachineGuid == guid;
            if (!macs.IsNullOrEmpty()) exp &= _.MACs == macs;

            if (exp.IsEmpty) return new List<Node>();

            return FindAll(exp);
        }

        /// <summary>高级查询</summary>
        /// <param name="provinceId">省份</param>
        /// <param name="cityId">城市</param>
        /// <param name="version">版本</param>
        /// <param name="enable"></param>
        /// <param name="start"></param>
        /// <param name="end"></param>
        /// <param name="key"></param>
        /// <param name="page"></param>
        /// <returns></returns>
        public static IList<Node> Search(Int32 provinceId, Int32 cityId, String version, Boolean? enable, DateTime start, DateTime end, String key, PageParameter page)
        {
            var exp = new WhereExpression();

            if (provinceId >= 0) exp &= _.ProvinceID == provinceId;
            if (cityId >= 0) exp &= _.CityID == cityId;
            if (!version.IsNullOrEmpty()) exp &= _.Version == version;
            if (enable != null) exp &= _.Enable == enable.Value;

            //exp &= _.CreateTime.Between(start, end);
            exp &= _.LastLogin.Between(start, end);

            if (!key.IsNullOrEmpty()) exp &= SearchWhereByKeys(key);

            return FindAll(exp, page);
        }

        internal static IList<Node> SearchByCreateDate(DateTime date)
        {
            // 先用带有索引的UpdateTime过滤一次
            return FindAll(_.UpdateTime >= date & _.CreateTime.Between(date, date));
        }

        internal static IDictionary<Int32, Int32> SearchCountByCreateDate(DateTime date)
        {
            var exp = new WhereExpression();
            exp &= _.CreateTime < date.AddDays(1);
            var list = FindAll(exp.GroupBy(_.ProvinceID), null, _.ID.Count() & _.ProvinceID, 0, 0);
            return list.ToDictionary(e => e.ProvinceID, e => e.ID);
        }
        #endregion

        #region 扩展操作
        #endregion

        #region 业务
        /// <summary>根据编码查询或添加</summary>
        /// <param name="code"></param>
        /// <returns></returns>
        public static Node GetOrAdd(String code) => GetOrAdd(code, FindByCode, k => new Node { Code = k, Enable = true });
        #endregion
    }
}