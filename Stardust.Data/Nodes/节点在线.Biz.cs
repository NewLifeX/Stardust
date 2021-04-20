using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Web.Script.Serialization;
using System.Xml.Serialization;
using NewLife;
using NewLife.Data;
using NewLife.Serialization;
using Stardust.Models;
using XCode;
using XCode.Membership;

namespace Stardust.Data.Nodes
{
    /// <summary>节点在线</summary>
    public partial class NodeOnline : Entity<NodeOnline>
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

        /// <summary>根据节点查找所有在线记录</summary>
        /// <param name="nodeId"></param>
        /// <returns></returns>
        public static IList<NodeOnline> FindAllByNodeId(Int32 nodeId) => FindAll(_.NodeID == nodeId);
        #endregion

        #region 高级查询
        /// <summary>查询满足条件的记录集，分页、排序</summary>
        /// <param name="nodeId">节点</param>
        /// <param name="provinceId">省份</param>
        /// <param name="cityId">城市</param>
        /// <param name="category">类别</param>
        /// <param name="start">开始时间</param>
        /// <param name="end">结束时间</param>
        /// <param name="key">关键字</param>
        /// <param name="page">分页排序参数，同时返回满足条件的总记录数</param>
        /// <returns>实体集</returns>
        public static IList<NodeOnline> Search(Int32 nodeId, Int32 provinceId, Int32 cityId, String category, DateTime start, DateTime end, String key, PageParameter page)
        {
            var exp = new WhereExpression();

            if (nodeId >= 0) exp &= _.NodeID == nodeId;
            if (provinceId >= 0) exp &= _.ProvinceID == provinceId;
            if (cityId >= 0) exp &= _.CityID == cityId;
            if (!category.IsNullOrEmpty()) exp &= _.Category == category;

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
        /// <param name="expire">超时时间，秒</param>
        /// <returns></returns>
        public static IList<NodeOnline> ClearExpire(TimeSpan expire)
        {
            if (Meta.Count == 0) return null;

            // 10分钟不活跃将会被删除
            var exp = _.UpdateTime < DateTime.Now.Subtract(expire);
            var list = FindAll(exp, null, null, 0, 0);
            list.Delete();

            return list;
        }

        /// <summary>更新并保存在线状态</summary>
        /// <param name="di"></param>
        /// <param name="pi"></param>
        /// <param name="token"></param>
        public void Save(NodeInfo di, PingInfo pi, String token)
        {
            var olt = this;

            if (di != null)
            {
                olt.Fill(di);
                olt.LocalTime = di.Time.ToLocalTime();
                olt.MACs = di.Macs;
                //olt.COMs = di.COMs;
            }
            else
            {
                olt.Fill(pi);
            }

            olt.Token = token;
            olt.PingCount++;

            // 5秒内直接保存
            if (olt.CreateTime.AddSeconds(5) > DateTime.Now)
                olt.Save();
            else
                olt.SaveAsync();
        }

        /// <summary>填充节点信息</summary>
        /// <param name="di"></param>
        public void Fill(NodeInfo di)
        {
            var online = this;

            online.LocalTime = di.Time.ToLocalTime();
            online.MACs = di.Macs;
            //online.COMs = di.COMs;
            online.IP = di.IP;

            if (di.AvailableMemory > 0) online.AvailableMemory = (Int32)(di.AvailableMemory / 1024 / 1024);
            if (di.AvailableFreeSpace > 0) online.AvailableFreeSpace = (Int32)(di.AvailableFreeSpace / 1024 / 1024);
        }

        /// <summary>填充在线节点信息</summary>
        /// <param name="inf"></param>
        private void Fill(PingInfo inf)
        {
            var olt = this;

            if (inf.AvailableMemory > 0) olt.AvailableMemory = (Int32)(inf.AvailableMemory / 1024 / 1024);
            if (inf.AvailableFreeSpace > 0) olt.AvailableFreeSpace = (Int32)(inf.AvailableFreeSpace / 1024 / 1024);
            if (inf.CpuRate > 0) olt.CpuRate = inf.CpuRate;
            if (inf.Temperature > 0) olt.Temperature = inf.Temperature;
            if (inf.Battery > 0) olt.Battery = inf.Battery;
            if (inf.UplinkSpeed > 0) olt.UplinkSpeed = (Int64)inf.UplinkSpeed;
            if (inf.DownlinkSpeed > 0) olt.DownlinkSpeed = (Int64)inf.DownlinkSpeed;
            if (inf.ProcessCount > 0) olt.ProcessCount = inf.ProcessCount;
            if (inf.TcpConnections > 0) olt.TcpConnections = inf.TcpConnections;
            if (inf.TcpTimeWait > 0) olt.TcpTimeWait = inf.TcpTimeWait;
            if (inf.TcpCloseWait > 0) olt.TcpCloseWait = inf.TcpCloseWait;
            if (inf.Uptime > 0) olt.Uptime = inf.Uptime;
            if (inf.Delay > 0) olt.Delay = inf.Delay;

            var dt = inf.Time.ToDateTime().ToLocalTime();
            if (dt.Year > 2000)
            {
                olt.LocalTime = dt;
                olt.Offset = (Int32)Math.Round((dt - DateTime.Now).TotalSeconds);
            }

            if (!inf.Processes.IsNullOrEmpty()) olt.Processes = inf.Processes;
            if (!inf.Macs.IsNullOrEmpty()) olt.MACs = inf.Macs;
            //if (!inf.COMs.IsNullOrEmpty()) olt.COMs = inf.COMs;
            if (!inf.IP.IsNullOrEmpty()) olt.IP = inf.IP;

            // 插入节点数据
            var data = new NodeData
            {
                NodeID = olt.NodeID,
                Name = olt.Name,
                AvailableMemory = olt.AvailableMemory,
                AvailableFreeSpace = olt.AvailableFreeSpace,
                CpuRate = inf.CpuRate,
                Temperature = inf.Temperature,
                Battery = inf.Battery,
                UplinkSpeed = (Int64)inf.UplinkSpeed,
                DownlinkSpeed = (Int64)inf.DownlinkSpeed,
                ProcessCount = inf.ProcessCount,
                TcpConnections = inf.TcpConnections,
                TcpTimeWait = inf.TcpTimeWait,
                TcpCloseWait = inf.TcpCloseWait,
                Uptime = inf.Uptime,
                Delay = inf.Delay,
                LocalTime = dt,
                Offset = olt.Offset,
                Data = inf.ToJson(),
                Creator = Environment.MachineName,
            };

            data.SaveAsync();
        }
        #endregion
    }
}