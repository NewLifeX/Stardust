using NewLife;
using NewLife.Data;
using NewLife.Log;
using System;
using System.Collections.Generic;
using System.Web.Script.Serialization;
using System.Xml.Serialization;
using XCode;
using XCode.Cache;
using XCode.Membership;

namespace Stardust.Data.Nodes;

/// <summary>节点历史</summary>
public partial class NodeHistory : Entity<NodeHistory>
{
    #region 对象操作
    static NodeHistory()
    {
        Meta.Table.DataTable.InsertOnly = true;

        Meta.Modules.Add<TimeModule>();
        Meta.Modules.Add<IPModule>();

        // 针对Mysql启用压缩表
        var table = Meta.Table.DataTable;
        table.Properties["ROW_FORMAT"] = "COMPRESSED";
        table.Properties["KEY_BLOCK_SIZE"] = "4";
    }

    /// <summary>插入或修改时</summary>
    /// <param name="isNew"></param>
    public override void Valid(Boolean isNew)
    {
        // 截断日志
        var len = _.Remark.Length;
        if (len > 0 && !Remark.IsNullOrEmpty() && len > 0 && Remark.Length > len) Remark = Remark[..len];

        // 建议先调用基类方法，基类方法会做一些统一处理
        base.Valid(isNew);

        if (TraceId.IsNullOrEmpty()) TraceId = DefaultSpan.Current?.TraceId;
    }
    #endregion

    #region 扩展属性
    /// <summary>节点</summary>
    [XmlIgnore, ScriptIgnore]
    public Node Node => Extends.Get(nameof(Node), k => Node.FindByID(NodeID));

    /// <summary>节点</summary>
    [Map(__.NodeID, typeof(Node), "ID")]
    public String NodeName => Node + "";

    /// <summary>省份</summary>
    [XmlIgnore, ScriptIgnore]
    public Area Province => Extends.Get(nameof(Province), k => Area.FindByID(ProvinceID));

    /// <summary>省份名</summary>
    [Map(__.ProvinceID)]
    public String ProvinceName => Province + "";

    /// <summary>城市</summary>
    [XmlIgnore, ScriptIgnore]
    public Area City => Extends.Get(nameof(City), k => Area.FindByID(CityID));

    /// <summary>城市名</summary>
    [Map(__.CityID)]
    public String CityName => City?.Path ?? Province?.Path;
    #endregion

    #region 扩展查询
    /// <summary>根据编号查找</summary>
    /// <param name="id">编号</param>
    /// <returns>实体对象</returns>
    public static NodeHistory FindById(Int32 id)
    {
        if (id <= 0) return null;

        //// 实体缓存
        //if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.ID == id);

        //// 单对象缓存
        //return Meta.SingleCache[id];

        return Find(_.Id == id);
    }

    /// <summary>根据节点、操作查找</summary>
    /// <param name="nodeId">节点</param>
    /// <param name="action">操作</param>
    /// <returns>实体列表</returns>
    public static IList<NodeHistory> FindAllByNodeIDAndAction(Int32 nodeId, String action)
    {
        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.NodeID == nodeId && e.Action.EqualIgnoreCase(action));

        return FindAll(_.NodeID == nodeId & _.Action == action);
    }
    #endregion

    #region 高级查询
    /// <summary>高级搜索</summary>
    /// <param name="nodeId"></param>
    /// <param name="provinceId">省份</param>
    /// <param name="cityId">城市</param>
    /// <param name="action"></param>
    /// <param name="success"></param>
    /// <param name="start"></param>
    /// <param name="end"></param>
    /// <param name="key"></param>
    /// <param name="page"></param>
    /// <returns></returns>
    public static IList<NodeHistory> Search(Int32 nodeId, Int32 provinceId, Int32 cityId, String action, Boolean? success, DateTime start, DateTime end, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (nodeId > 0) exp &= _.NodeID == nodeId;
        if (provinceId >= 0) exp &= _.ProvinceID == provinceId;
        if (cityId >= 0) exp &= _.CityID == cityId;
        if (!action.IsNullOrEmpty()) exp &= _.Action.In(action.Split(","));
        if (success != null) exp &= _.Success == success;

        // 主键带有时间戳
        exp &= _.Id.Between(start, end, Meta.Factory.Snow);

        if (!key.IsNullOrEmpty())
        {
            exp &= _.Name.Contains(key) | _.Remark.Contains(key) | _.CreateIP == key;
        }

        return FindAll(exp, page);
    }
    #endregion

    #region 扩展操作
    /// <summary>类别名实体缓存，异步，缓存10分钟</summary>
    static FieldCache<NodeHistory> ActionCache = new FieldCache<NodeHistory>(__.Action);

    /// <summary>获取所有类别名称</summary>
    /// <returns></returns>
    public static IDictionary<String, String> FindAllActionName() => ActionCache.FindAllName();
    #endregion

    #region 业务
    /// <summary>创建日志</summary>
    /// <param name="node"></param>
    /// <param name="action"></param>
    /// <param name="success"></param>
    /// <param name="remark"></param>
    /// <param name="creator"></param>
    /// <param name="ip"></param>
    /// <returns></returns>
    public static NodeHistory Create(Node node, String action, Boolean success, String remark, String creator, String ip)
    {
        node ??= new Node();

        var history = new NodeHistory
        {
            NodeID = node.ID,
            Name = node.Name,
            Action = action,
            Success = success,

            ProvinceID = node.ProvinceID,
            CityID = node.CityID,

            Version = node.Version,
            CompileTime = node.CompileTime,

            Remark = remark,

            Creator = creator,
            CreateTime = DateTime.Now,
            CreateIP = ip,
        };

        //history.SaveAsync();

        return history;
    }

    static Lazy<FieldCache<NodeHistory>> NameCache = new(() => new FieldCache<NodeHistory>(__.Action));
    /// <summary>获取所有分类名称</summary>
    /// <returns></returns>
    public static IDictionary<String, String> FindAllAction() => NameCache.Value.FindAllName();

    /// <summary>删除指定日期之前的数据</summary>
    /// <param name="date"></param>
    /// <returns></returns>
    public static Int32 DeleteBefore(DateTime date) => Delete(_.Id < Meta.Factory.Snow.GetId(date));
    #endregion
}