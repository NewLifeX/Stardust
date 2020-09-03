using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.Serialization;
using System.Xml.Serialization;
using NewLife;
using NewLife.Data;
using XCode;
using XCode.Membership;

namespace Stardust.Data.Nodes
{
    /// <summary>节点数据。保存设备上来的一些数据，如心跳状态</summary>
    public partial class NodeData : Entity<NodeData>
    {
        #region 对象操作
        static NodeData()
        {
            // 累加字段，生成 Update xx Set Count=Count+1234 Where xxx
            //var df = Meta.Factory.AdditionalFields;
            //df.Add(nameof(NodeID));

            // 过滤器 UserModule、TimeModule、IPModule
            Meta.Modules.Add<TimeModule>();
            Meta.Modules.Add<IPModule>();
        }

        /// <summary>验证数据，通过抛出异常的方式提示验证失败。</summary>
        /// <param name="isNew">是否插入</param>
        public override void Valid(Boolean isNew)
        {
            // 如果没有脏数据，则不需要进行任何处理
            if (!HasDirty) return;

            // 在新插入数据或者修改了指定字段时进行修正
            //if (isNew && !Dirtys[nameof(CreateTime)]) CreateTime = DateTime.Now;
            //if (isNew && !Dirtys[nameof(CreateIP)]) CreateIP = ManageProvider.UserHost;
        }

        ///// <summary>首次连接数据库时初始化数据，仅用于实体类重载，用户不应该调用该方法</summary>
        //[EditorBrowsable(EditorBrowsableState.Never)]
        //protected override void InitData()
        //{
        //    // InitData一般用于当数据表没有数据时添加一些默认数据，该实体类的任何第一次数据库操作都会触发该方法，默认异步调用
        //    if (Meta.Session.Count > 0) return;

        //    if (XTrace.Debug) XTrace.WriteLine("开始初始化NodeData[节点数据]数据……");

        //    var entity = new NodeData();
        //    entity.ID = 0;
        //    entity.NodeID = 0;
        //    entity.Name = "abc";
        //    entity.Memory = 0;
        //    entity.AvailableMemory = 0;
        //    entity.AvailableFreeSpace = 0;
        //    entity.CpuRate = 0.0;
        //    entity.Temperature = 0.0;
        //    entity.Delay = 0;
        //    entity.Offset = 0;
        //    entity.LocalTime = DateTime.Now;
        //    entity.Uptime = 0;
        //    entity.Data = "abc";
        //    entity.CompileTime = DateTime.Now;
        //    entity.Creator = "abc";
        //    entity.CreateTime = DateTime.Now;
        //    entity.CreateIP = "abc";
        //    entity.Insert();

        //    if (XTrace.Debug) XTrace.WriteLine("完成初始化NodeData[节点数据]数据！");
        //}

        ///// <summary>已重载。基类先调用Valid(true)验证数据，然后在事务保护内调用OnInsert</summary>
        ///// <returns></returns>
        //public override Int32 Insert()
        //{
        //    return base.Insert();
        //}

        ///// <summary>已重载。在事务保护范围内处理业务，位于Valid之后</summary>
        ///// <returns></returns>
        //protected override Int32 OnDelete()
        //{
        //    return base.OnDelete();
        //}
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
        #endregion

        #region 扩展查询
        /// <summary>根据编号查找</summary>
        /// <param name="id">编号</param>
        /// <returns>实体对象</returns>
        public static NodeData FindByID(Int32 id)
        {
            if (id <= 0) return null;

            // 实体缓存
            if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.ID == id);

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
            var flow = Meta.Factory.FlowId;
            if (flow != null)
                exp &= _.ID.Between(start, end, flow);
            else
                exp &= _.CreateTime.Between(start, end);

            if (!key.IsNullOrEmpty()) exp &= _.Name.Contains(key) | _.Data.Contains(key) | _.Creator.Contains(key) | _.CreateIP.Contains(key);

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
        #endregion
    }
}