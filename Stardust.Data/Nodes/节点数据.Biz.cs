using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.Serialization;
using System.Xml.Serialization;
using NewLife;
using NewLife.Data;
using XCode;

namespace Stardust.Data.Nodes;

/// <summary>节点数据。保存设备上来的一些数据，如心跳状态</summary>
public partial class NodeData : Entity<NodeData>
{
    #region 对象操作
    static NodeData()
    {
        Meta.Factory.Table.DataTable.InsertOnly = true;

        // 累加字段，生成 Update xx Set Count=Count+1234 Where xxx
        //var df = Meta.Factory.AdditionalFields;
        //df.Add(nameof(NodeID));

        // 过滤器 UserModule、TimeModule、IPModule
        Meta.Modules.Add<TimeModule>();
        Meta.Modules.Add<IPModule>();

        // 针对Mysql启用压缩表
        var table = Meta.Table.DataTable;
        table.Properties["ROW_FORMAT"] = "COMPRESSED";
        table.Properties["KEY_BLOCK_SIZE"] = "4";
    }

    /// <summary>验证数据，通过抛出异常的方式提示验证失败。</summary>
    /// <param name="isNew">是否插入</param>
    public override void Valid(Boolean isNew)
    {
        // 如果没有脏数据，则不需要进行任何处理
        if (!HasDirty) return;

        base.Valid(isNew);

        // 在新插入数据或者修改了指定字段时进行修正
        //if (isNew && !Dirtys[nameof(CreateTime)]) CreateTime = DateTime.Now;
        //if (isNew && !Dirtys[nameof(CreateIP)]) CreateIP = ManageProvider.UserHost;
    }
    #endregion

    #region 扩展属性
    /// <summary>节点</summary>
    [XmlIgnore, IgnoreDataMember]
    //[ScriptIgnore]
    public Node Node => Extends.Get(nameof(Node), k => Node.FindByID(NodeID));

    /// <summary>节点</summary>
    [XmlIgnore, IgnoreDataMember]
    //[ScriptIgnore]
    [DisplayName("节点")]
    [Map(nameof(NodeID), typeof(Node), "ID")]
    public String NodeName => Node?.Name;

    /// <summary>开机时间</summary>
    [Map(nameof(Uptime))]
    public String UptimeName => TimeSpan.FromSeconds(Uptime).ToString().TrimEnd("0000").TrimStart("00:");
    #endregion

    #region 扩展查询
    /// <summary>根据编号查找</summary>
    /// <param name="id">编号</param>
    /// <returns>实体对象</returns>
    public static NodeData FindById(Int32 id)
    {
        if (id <= 0) return null;

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.Id == id);

        // 单对象缓存
        return Meta.SingleCache[id];

        //return Find(_.ID == id);
    }

    /// <summary>根据节点查找</summary>
    /// <param name="nodeId">节点</param>
    /// <returns>实体列表</returns>
    public static IList<NodeData> FindAllByNodeID(Int32 nodeId)
    {
        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.NodeID == nodeId);

        return FindAll(_.NodeID == nodeId);
    }

    /// <summary>获取最后一条节点数据</summary>
    /// <param name="nodeId"></param>
    /// <returns></returns>
    public static NodeData FindLast(Int32 nodeId)
    {
        if (nodeId <= 0) return null;

        return FindAll(_.NodeID == nodeId, _.Id.Desc(), null, 0, 1).FirstOrDefault();
    }
    #endregion

    #region 高级查询
    /// <summary>高级查询</summary>
    /// <param name="nodeId">节点</param>
    /// <param name="start">创建时间开始</param>
    /// <param name="end">创建时间结束</param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
    /// <returns>实体列表</returns>
    public static IList<NodeData> Search(Int32 nodeId, DateTime start, DateTime end, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (nodeId >= 0) exp &= _.NodeID == nodeId;

        // 主键带有时间戳
        var flow = Meta.Factory.Snow;
        if (flow != null)
            exp &= _.Id.Between(start, end, flow);
        else
            exp &= _.CreateTime.Between(start, end);

        if (!key.IsNullOrEmpty()) exp &= _.Name.Contains(key) | _.Creator.Contains(key) | _.CreateIP.Contains(key);

        return FindAll(exp, page);
    }

    // Select Count(ID) as ID,Category From NodeData Where CreateTime>'2020-01-24 00:00:00' Group By Category Order By ID Desc limit 20
    //static readonly FieldCache<NodeData> _CategoryCache = new FieldCache<NodeData>(nameof(Category))
    //{
    //Where = _.CreateTime > DateTime.Today.AddDays(-30) & Expression.Empty
    //};

    ///// <summary>获取类别列表，字段缓存10分钟，分组统计数据最多的前20种，用于魔方前台下拉选择</summary>
    ///// <returns></returns>
    //public static IDictionary<String, String> GetCategoryList() => _CategoryCache.FindAllName();
    #endregion

    #region 业务操作
    /// <summary>删除指定日期之前的数据</summary>
    /// <param name="date"></param>
    /// <returns></returns>
    public static Int32 DeleteBefore(DateTime date) => Delete(_.Id < Meta.Factory.Snow.GetId(date));
    #endregion
}