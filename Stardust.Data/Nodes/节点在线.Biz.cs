using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Web.Script.Serialization;
using System.Xml.Serialization;
using NewLife.Data;
using XCode;
using XCode.Membership;

namespace Stardust.Data.Nodes
{
    /// <summary>节点在线</summary>
    public partial class NodeOnline : EntityBase<NodeOnline>
    {
        #region 对象操作
        static NodeOnline()
        {
            var df = Meta.Factory.AdditionalFields;
            df.Add(__.PingCount);

            Meta.Modules.Add<TimeModule>();
            Meta.Modules.Add<IPModule>();

            var sc = Meta.SingleCache;
            sc.FindSlaveKeyMethod = k => Find(_.SessionID == k);
            sc.GetSlaveKeyMethod = e => e.SessionID;
        }

        /// <summary>校验数据</summary>
        /// <param name="isNew"></param>
        public override void Valid(Boolean isNew)
        {
            // 截取部分进程字段，避免过长无法保存
            if (Processes != null && Processes.Length > 2000) Processes = Processes.Substring(0, 1999);
            if (MACs != null && MACs.Length > 200) MACs = MACs.Substring(0, 1999);
            //if (COMs != null && COMs.Length > 200) COMs = COMs.Substring(0, 199);

            base.Valid(isNew);
        }
        #endregion

        #region 扩展属性
        /// <summary>节点</summary>
        [XmlIgnore, ScriptIgnore]
        public Node Node => Extends.Get(nameof(Node), k => Node.FindByID(NodeID));

        /// <summary>节点</summary>
        [Map(__.NodeID)]
        public String NodeName => Node + "";

        /// <summary>省份</summary>
        [XmlIgnore, IgnoreDataMember]
        public Area Province => Extends.Get(nameof(Province), k => Area.FindByID(ProvinceID));

        /// <summary>省份名</summary>
        [Map(__.ProvinceID)]
        public String ProvinceName => Province + "";

        /// <summary>城市</summary>
        [XmlIgnore, IgnoreDataMember]
        public Area City => Extends.Get(nameof(City), k => Area.FindByID(CityID));

        /// <summary>城市名</summary>
        [Map(__.CityID)]
        public String CityName => City?.Path;
        #endregion

        #region 扩展查询
        /// <summary>根据会话查找</summary>
        /// <param name="deviceid">会话</param>
        /// <returns></returns>
        public static NodeOnline FindByNodeID(Int32 deviceid) => Find(__.NodeID, deviceid);

        /// <summary>根据会话查找</summary>
        /// <param name="sessionid">会话</param>
        /// <param name="cache">是否走缓存</param>
        /// <returns></returns>
        public static NodeOnline FindBySessionID(String sessionid, Boolean cache = true)
        {
            if (!cache) return Find(_.SessionID == sessionid);

            return Meta.SingleCache.GetItemWithSlaveKey(sessionid) as NodeOnline;
        }
        #endregion

        #region 高级查询
        /// <summary>查询满足条件的记录集，分页、排序</summary>
        /// <param name="nodeId">节点</param>
        /// <param name="provinceId">省份</param>
        /// <param name="cityId">城市</param>
        /// <param name="start">开始时间</param>
        /// <param name="end">结束时间</param>
        /// <param name="key">关键字</param>
        /// <param name="page">分页排序参数，同时返回满足条件的总记录数</param>
        /// <returns>实体集</returns>
        public static IList<NodeOnline> Search(Int32 nodeId, Int32 provinceId, Int32 cityId, DateTime start, DateTime end, String key, PageParameter page)
        {
            var exp = new WhereExpression();

            if (nodeId >= 0) exp &= _.NodeID == nodeId;
            if (provinceId >= 0) exp &= _.ProvinceID == provinceId;
            if (cityId >= 0) exp &= _.CityID == cityId;

            exp &= _.CreateTime.Between(start, end);

            if (!key.IsNullOrEmpty()) exp &= (_.Name.Contains(key) | _.SessionID.Contains(key));

            return FindAll(exp, page);
        }

        /// <summary>根据产品，分组统计在线数</summary>
        /// <returns></returns>
        public static IDictionary<Int32, Int32> SearchGroupByProvince()
        {
            var list = FindAll(_.ProvinceID.GroupBy(), null, _.ID.Count() & _.ProvinceID);
            return list.ToDictionary(e => e.ProvinceID, e => e.ID);
        }
        #endregion

        #region 业务操作
        /// <summary>根据编码查询或添加</summary>
        /// <param name="sessionid"></param>
        /// <returns></returns>
        public static NodeOnline GetOrAdd(String sessionid) => GetOrAdd(sessionid, FindBySessionID, k => new NodeOnline { SessionID = k });

        /// <summary>删除过期，指定过期时间</summary>
        /// <param name="secTimeout">超时时间，秒</param>
        /// <returns></returns>
        public static IList<NodeOnline> ClearExpire(Int32 secTimeout)
        {
            if (Meta.Count == 0) return null;

            // 10分钟不活跃将会被删除
            var exp = _.UpdateTime < DateTime.Now.AddSeconds(-secTimeout);
            var list = FindAll(exp, null, null, 0, 0);
            list.Delete();

            return list;
        }
        #endregion
    }
}