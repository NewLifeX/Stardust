using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Web.Script.Serialization;
using System.Xml.Serialization;
using NewLife;
using NewLife.Data;
using NewLife.Log;
using NewLife.Serialization;
using Stardust.Models;
using XCode;
using XCode.Membership;

namespace Stardust.Data.Nodes;

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
        Meta.Modules.Add<TraceModule>();

        var sc = Meta.SingleCache;
        sc.FindSlaveKeyMethod = k => Find(_.SessionID == k);
        sc.GetSlaveKeyMethod = e => e.SessionID;
    }

    /// <summary>校验数据</summary>
    /// <param name="isNew"></param>
    public override void Valid(Boolean isNew)
    {
        // 截取部分进程字段，避免过长无法保存
        this.TrimExtraLong(__.Processes, __.MACs, __.DriveInfo);

        if (!Dirtys[nameof(MemoryUsed)] && Node != null) MemoryUsed = Node.Memory - AvailableMemory;
        if (!Dirtys[nameof(SpaceUsed)] && Node != null) SpaceUsed = Node.TotalSize - AvailableFreeSpace;

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
    public String CityName => City?.Path ?? Province?.Path;
    #endregion

    #region 扩展查询
    /// <summary>根据id查找</summary>
    /// <param name="id">会话</param>
    /// <returns></returns>
    public static NodeOnline FindById(Int32 id) => Find(__.ID, id);

    /// <summary>根据会话查找</summary>
    /// <param name="nodeId">会话</param>
    /// <returns></returns>
    public static NodeOnline FindByNodeId(Int32 nodeId) => Find(__.NodeID, nodeId);

    ///// <summary>根据会话查找</summary>
    ///// <param name="sessionid">会话</param>
    ///// <param name="cache">是否走缓存</param>
    ///// <returns></returns>
    //public static NodeOnline FindBySessionID(String sessionid, Boolean cache = true)
    //{
    //    if (!cache) return Find(_.SessionID == sessionid);

    //    return Meta.SingleCache.GetItemWithSlaveKey(sessionid) as NodeOnline;
    //}

    /// <summary>根据节点查找所有在线记录</summary>
    /// <param name="nodeId"></param>
    /// <returns></returns>
    public static IList<NodeOnline> FindAllByNodeId(Int32 nodeId) => FindAll(_.NodeID == nodeId);

    /// <summary>根据令牌查找</summary>
    /// <param name="token">令牌</param>
    /// <returns>实体列表</returns>
    public static IList<NodeOnline> FindAllByToken(String token)
    {
        if (token.IsNullOrEmpty()) return new List<NodeOnline>();

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.Token.EqualIgnoreCase(token));

        return FindAll(_.Token == token);
    }

    /// <summary>根据省份、城市查找</summary>
    /// <param name="provinceId">省份</param>
    /// <param name="cityId">城市</param>
    /// <returns>实体列表</returns>
    public static IList<NodeOnline> FindAllByProvinceIDAndCityID(Int32 provinceId, Int32 cityId)
    {
        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.ProvinceID == provinceId && e.CityID == cityId);

        return FindAll(_.ProvinceID == provinceId & _.CityID == cityId);
    }

    /// <summary>根据项目查找</summary>
    /// <param name="projectId">项目</param>
    /// <returns>实体列表</returns>
    public static IList<NodeOnline> FindAllByProjectId(Int32 projectId)
    {
        if (projectId <= 0) return new List<NodeOnline>();

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.ProjectId == projectId);

        return FindAll(_.ProjectId == projectId);
    }
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
    public static IList<NodeOnline> Search(Int32 projectId, Int32 nodeId, Int32 provinceId, Int32 cityId, String category, DateTime start, DateTime end, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (projectId >= 0) exp &= _.ProjectId == projectId;
        if (nodeId >= 0) exp &= _.NodeID == nodeId;
        if (provinceId >= 0) exp &= _.ProvinceID == provinceId;
        if (cityId >= 0) exp &= _.CityID == cityId;
        if (!category.IsNullOrEmpty()) exp &= _.Category == category;

        exp &= _.CreateTime.Between(start, end);

        if (!key.IsNullOrEmpty()) exp &= _.Name.Contains(key) | _.Data.Contains(key) | _.SessionID.Contains(key);

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
    /// <param name="ip"></param>
    public void Save(NodeInfo di, PingInfo pi, String token, String ip)
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
            olt.CreateData(pi, ip);
        }

        olt.Token = token;
        olt.PingCount++;
        olt.UpdateIP = ip;
        olt.TraceId = DefaultSpan.Current?.TraceId;

        // 5秒内直接保存
        if (olt.CreateTime.AddSeconds(5) > DateTime.Now)
            olt.Save();
        else
            olt.SaveAsync();
    }

    /// <summary>填充节点信息</summary>
    /// <param name="inf"></param>
    public void Fill(NodeInfo inf)
    {
        var online = this;

        online.LocalTime = inf.Time.ToLocalTime();
        online.MACs = inf.Macs;
        //online.COMs = di.COMs;
        online.IP = inf.IP;
        online.Gateway = inf.Gateway;
        online.Dns = inf.Dns;

        if (inf.AvailableMemory > 0) online.AvailableMemory = (Int32)(inf.AvailableMemory / 1024 / 1024);
        if (inf.AvailableFreeSpace > 0) online.AvailableFreeSpace = (Int32)(inf.AvailableFreeSpace / 1024 / 1024);
        if (!online.DriveInfo.IsNullOrEmpty()) online.DriveInfo = online.DriveInfo;
    }

    /// <summary>填充在线节点信息</summary>
    /// <param name="inf"></param>
    private void Fill(PingInfo inf)
    {
        var online = this;

        if (inf.AvailableMemory > 0) online.AvailableMemory = (Int32)(inf.AvailableMemory / 1024 / 1024);
        if (inf.AvailableFreeSpace > 0) online.AvailableFreeSpace = (Int32)(inf.AvailableFreeSpace / 1024 / 1024);
        if (!inf.DriveInfo.IsNullOrEmpty()) online.DriveInfo = inf.DriveInfo;
        if (inf.CpuRate > 0) online.CpuRate = inf.CpuRate;
        if (inf.Temperature > 0) online.Temperature = inf.Temperature;
        if (inf.Battery > 0) online.Battery = inf.Battery;
        /*if (inf.Signal > 0)*/
        online.Signal = inf.Signal;
        if (inf.UplinkSpeed > 0) online.UplinkSpeed = (Int64)inf.UplinkSpeed;
        if (inf.DownlinkSpeed > 0) online.DownlinkSpeed = (Int64)inf.DownlinkSpeed;
        if (inf.ProcessCount > 0) online.ProcessCount = inf.ProcessCount;
        if (inf.TcpConnections > 0) online.TcpConnections = inf.TcpConnections;
        if (inf.TcpTimeWait > 0) online.TcpTimeWait = inf.TcpTimeWait;
        if (inf.TcpCloseWait > 0) online.TcpCloseWait = inf.TcpCloseWait;
        if (inf.Uptime > 0) online.Uptime = inf.Uptime;
        if (inf.Delay > 0) online.Delay = inf.Delay;

        var dt = inf.Time.ToDateTime().ToLocalTime();
        if (dt.Year > 2000)
        {
            online.LocalTime = dt;
            //olt.Offset = (Int32)Math.Round((dt - DateTime.Now).TotalSeconds);
            online.Offset = (Int32)(inf.Time + (Delay / 2) - DateTime.UtcNow.ToLong());
        }

        if (!inf.Processes.IsNullOrEmpty()) online.Processes = inf.Processes;
        if (!inf.Macs.IsNullOrEmpty()) online.MACs = inf.Macs;
        //if (!inf.COMs.IsNullOrEmpty()) olt.COMs = inf.COMs;
        if (!inf.IP.IsNullOrEmpty()) online.IP = inf.IP;
        if (!inf.Gateway.IsNullOrEmpty()) online.Gateway = inf.Gateway;

        //olt.Data = inf.ToJson();
        var dic = inf.ToDictionary();
        dic.Remove("Processes");
        online.Data = dic.ToJson();
    }

    private void CreateData(PingInfo inf, String ip)
    {
        var olt = this;

        var dt = inf.Time.ToDateTime().ToLocalTime();

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
            Signal = inf.Signal,
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
            CreateIP = ip,
            Creator = Environment.MachineName,
        };

        //data.SaveAsync();
        data.Insert();
    }
    #endregion
}