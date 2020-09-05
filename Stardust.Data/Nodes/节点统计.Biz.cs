using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Xml.Serialization;
using NewLife;
using NewLife.Data;
using XCode;
using XCode.Membership;

namespace Stardust.Data.Nodes
{
    /// <summary>节点统计。每日统计</summary>
    public partial class NodeStat : Entity<NodeStat>
    {
        #region 对象操作
        static NodeStat()
        {
            // 累加字段，生成 Update xx Set Count=Count+1234 Where xxx
            //var df = Meta.Factory.AdditionalFields;
            //df.Add(__.Total);

            // 过滤器 UserModule、TimeModule、IPModule
            Meta.Modules.Add<TimeModule>();
        }

        /// <summary>验证数据，通过抛出异常的方式提示验证失败。</summary>
        /// <param name="isNew">是否插入</param>
        public override void Valid(Boolean isNew)
        {
            // 如果没有脏数据，则不需要进行任何处理
            if (!HasDirty) return;

            // 在新插入数据或者修改了指定字段时进行修正
            //if (isNew && !Dirtys[nameof(CreateTime)]) CreateTime = DateTime.Now;
            //if (!Dirtys[nameof(UpdateTime)]) UpdateTime = DateTime.Now;

            // 检查唯一索引
            // CheckExist(isNew, __.StatDate);
        }

        ///// <summary>首次连接数据库时初始化数据，仅用于实体类重载，用户不应该调用该方法</summary>
        //[EditorBrowsable(EditorBrowsableState.Never)]
        //protected internal override void InitData()
        //{
        //    // InitData一般用于当数据表没有数据时添加一些默认数据，该实体类的任何第一次数据库操作都会触发该方法，默认异步调用
        //    if (Meta.Session.Count > 0) return;

        //    if (XTrace.Debug) XTrace.WriteLine("开始初始化NodeStat[节点统计]数据……");

        //    var entity = new NodeStat();
        //    entity.ID = 0;
        //    entity.StatDate = DateTime.Now;
        //    entity.Total = 0;
        //    entity.Actives = 0;
        //    entity.News = 0;
        //    entity.Registers = 0;
        //    entity.MaxOnline = 0;
        //    entity.MaxOnlineTime = DateTime.Now;
        //    entity.CreateTime = DateTime.Now;
        //    entity.UpdateTime = DateTime.Now;
        //    entity.Remark = "abc";
        //    entity.Insert();

        //    if (XTrace.Debug) XTrace.WriteLine("完成初始化NodeStat[节点统计]数据！");
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
        /// <summary>省份</summary>
        [XmlIgnore, IgnoreDataMember]
        public Area Province => Extends.Get(nameof(Province), k => Area.FindByID(AreaID));

        /// <summary>省份名</summary>
        [Map(__.AreaID)]
        public String ProvinceName => Province?.Name;
        #endregion

        #region 扩展查询
        /// <summary>根据编号查找</summary>
        /// <param name="id">编号</param>
        /// <returns>实体对象</returns>
        public static NodeStat FindByID(Int32 id)
        {
            if (id <= 0) return null;

            // 实体缓存
            if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.ID == id);

            // 单对象缓存
            return Meta.SingleCache[id];

            //return Find(_.ID == id);
        }

        /// <summary>根据日期查找</summary>
        /// <param name="date"></param>
        /// <returns></returns>
        public static IList<NodeStat> FindAllByDate(DateTime date) => FindAll(_.StatDate == date);
        #endregion

        #region 高级查询
        /// <summary>高级查询</summary>
        /// <param name="areaId">省份</param>
        /// <param name="start">统计日期开始</param>
        /// <param name="end">统计日期结束</param>
        /// <param name="key">关键字</param>
        /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
        /// <returns>实体列表</returns>
        public static IList<NodeStat> Search(Int32 areaId, DateTime start, DateTime end, String key, PageParameter page)
        {
            var exp = new WhereExpression();

            exp &= _.StatDate.Between(start, end);
            if (areaId >= 0) exp &= _.AreaID == areaId;
            if (!key.IsNullOrEmpty()) exp &= _.Remark.Contains(key);

            return FindAll(exp, page);
        }

        // Select Count(ID) as ID,Category From NodeStat Where UpdateTime>'2020-01-24 00:00:00' Group By Category Order By ID Desc limit 20
        //static readonly FieldCache<NodeStat> _CategoryCache = new FieldCache<NodeStat>(_.Category)
        //{
        //Where = _.UpdateTime > DateTime.Today.AddDays(-30) & Expression.Empty
        //};

        ///// <summary>获取类别列表，字段缓存10分钟，分组统计数据最多的前20种，用于魔方前台下拉选择</summary>
        ///// <returns></returns>
        //public static IDictionary<String, String> GetCategoryList() => _CategoryCache.FindAllName();
        #endregion

        #region 业务操作
        /// <summary>统计指定日期的数据</summary>
        /// <param name="date"></param>
        public static void ProcessDate(DateTime date)
        {
            // 这一天的所有统计数据
            var sts = FindAllByDate(date);

            // 活跃数
            {
                var dic = Node.SearchGroupByLastLogin(date, date.AddDays(1));
                foreach (var item in dic)
                {
                    var st = GetStat(sts, item.Key, date);
                    st.Actives = item.Value;
                }
            }
            // 7天活跃数
            {
                var dic = Node.SearchGroupByLastLogin(date.AddDays(-7 + 1), date.AddDays(1));
                foreach (var item in dic)
                {
                    var st = GetStat(sts, item.Key, date);
                    st.T7Actives = item.Value;
                }
            }
            // 30天活跃数
            {
                var dic = Node.SearchGroupByLastLogin(date.AddDays(-30 + 1), date.AddDays(1));
                foreach (var item in dic)
                {
                    var st = GetStat(sts, item.Key, date);
                    st.T30Actives = item.Value;
                }
            }

            // 新增数
            {
                var dic = Node.SearchGroupByCreateTime(date, date.AddDays(1));
                foreach (var item in dic)
                {
                    var st = GetStat(sts, item.Key, date);
                    st.News = item.Value;
                }
            }
            // 7天新增
            {
                var dic = Node.SearchGroupByCreateTime(date.AddDays(-7 + 1), date.AddDays(1));
                foreach (var item in dic)
                {
                    var st = GetStat(sts, item.Key, date);
                    st.T7News = item.Value;
                }
            }
            // 30天新增
            {
                var dic = Node.SearchGroupByCreateTime(date.AddDays(-30 + 1), date.AddDays(1));
                foreach (var item in dic)
                {
                    var st = GetStat(sts, item.Key, date);
                    st.T30News = item.Value;
                }
            }

            // 注册数
            {
                var his = NodeHistory.Search(-1, -1, -1, "注册", null, date, date, null, null);
                var nodes = his.Select(e => e.NodeID).Distinct().Select(Node.FindByID).ToList();
                var dic = nodes.Where(e => e != null).GroupBy(e => e.ProvinceID).ToDictionary(e => e.Key, e => e.ToList());
                foreach (var item in dic)
                {
                    var st = GetStat(sts, item.Key, date);
                    st.Registers = item.Value.Count;
                }
            }

            // 总数
            {
                var dic = Node.SearchCountByCreateDate(date);
                foreach (var item in dic)
                {
                    var st = GetStat(sts, item.Key, date);
                    st.Total = item.Value;
                }
            }

            // 最高在线
            if (date == DateTime.Today)
            {
                var dic = NodeOnline.SearchGroupByProvince();
                foreach (var item in dic)
                {
                    if (item.Key == 0) continue;

                    var st = GetStat(sts, item.Key, date);
                    if (item.Value > st.MaxOnline)
                    {
                        st.MaxOnline = item.Value;
                        st.MaxOnlineTime = DateTime.Now;
                    }
                }
            }

            // 计算所有产品
            {
                var st = sts.FirstOrDefault(e => e.AreaID == 0);
                if (st == null)
                {
                    st = new NodeStat { StatDate = date, AreaID = 0 };
                    sts.Add(st);
                }

                var sts2 = sts.Where(e => e.AreaID != 0).ToList();
                st.Total = sts2.Sum(e => e.Total);
                st.Actives = sts2.Sum(e => e.Actives);
                st.T7Actives = sts2.Sum(e => e.T7Actives);
                st.T30Actives = sts2.Sum(e => e.T30Actives);
                st.News = sts2.Sum(e => e.News);
                st.T7News = sts2.Sum(e => e.T7News);
                st.T30News = sts2.Sum(e => e.T30News);
                st.Registers = sts2.Sum(e => e.Registers);

                var max = sts2.Sum(e => e.MaxOnline);
                if (max > st.MaxOnline)
                {
                    st.MaxOnline = max;
                    st.MaxOnlineTime = DateTime.Now;
                }
            }

            // 保存统计数据
            sts.Save(true);
        }

        private static NodeStat GetStat(IList<NodeStat> sts, Int32 areaId, DateTime date)
        {
            // 无法识别省份时，使用-1，因为0表示全国
            if (areaId == 0) areaId = -1;
            var st = sts.FirstOrDefault(e => e.AreaID == areaId);
            if (st == null)
            {
                st = new NodeStat { StatDate = date, AreaID = areaId };
                sts.Add(st);
            }

            return st;
        }
        #endregion
    }
    }