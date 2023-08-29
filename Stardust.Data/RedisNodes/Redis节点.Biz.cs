using System;
using System.Collections.Generic;
using System.ComponentModel;
using NewLife;
using NewLife.Data;
using NewLife.Log;
using NewLife.Serialization;
using XCode;
using XCode.Cache;
using XCode.Membership;

namespace Stardust.Data.Nodes
{
    /// <summary>Redis节点。Redis管理</summary>
    public partial class RedisNode : Entity<RedisNode>
    {
        #region 对象操作
        static RedisNode()
        {
            // 累加字段，生成 Update xx Set Count=Count+1234 Where xxx
            //var df = Meta.Factory.AdditionalFields;
            //df.Add(nameof(MaxMemory));

            // 过滤器 UserModule、TimeModule、IPModule
            Meta.Modules.Add<UserModule>();
            Meta.Modules.Add<TimeModule>();
            Meta.Modules.Add<IPModule>();
        }

        /// <summary>验证并修补数据，通过抛出异常的方式提示验证失败。</summary>
        /// <param name="isNew">是否插入</param>
        public override void Valid(Boolean isNew)
        {
            // 如果没有脏数据，则不需要进行任何处理
            if (!HasDirty) return;

            // 建议先调用基类方法，基类方法会做一些统一处理
            base.Valid(isNew);

            // 在新插入数据或者修改了指定字段时进行修正
            // 处理当前已登录用户信息，可以由UserModule过滤器代劳
            /*var user = ManageProvider.User;
            if (user != null)
            {
                if (isNew && !Dirtys[nameof(CreateUserID)]) CreateUserID = user.ID;
                if (!Dirtys[nameof(UpdateUserID)]) UpdateUserID = user.ID;
            }*/
            //if (isNew && !Dirtys[nameof(CreateTime)]) CreateTime = DateTime.Now;
            //if (!Dirtys[nameof(UpdateTime)]) UpdateTime = DateTime.Now;
            //if (isNew && !Dirtys[nameof(CreateIP)]) CreateIP = ManageProvider.UserHost;
            //if (!Dirtys[nameof(UpdateIP)]) UpdateIP = ManageProvider.UserHost;

            // 检查唯一索引
            // CheckExist(isNew, nameof(Server));
        }

        /// <summary>首次连接数据库时初始化数据，仅用于实体类重载，用户不应该调用该方法</summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        protected override void InitData()
        {
            // InitData一般用于当数据表没有数据时添加一些默认数据，该实体类的任何第一次数据库操作都会触发该方法，默认异步调用
            if (Meta.Session.Count > 0) return;

            if (XTrace.Debug) XTrace.WriteLine("开始初始化RedisNode[Redis节点]数据……");

            var entity = new RedisNode
            {
                Name = "本地",
                Category = "默认",
                Server = "127.0.0.1:6379",
                Password = "",
                Enable = false,
                ScanQueue = true,
            };
            entity.Insert();

            if (XTrace.Debug) XTrace.WriteLine("完成初始化RedisNode[Redis节点]数据！");
        }
        #endregion

        #region 扩展属性
        #endregion

        #region 扩展查询
        /// <summary>根据编号查找</summary>
        /// <param name="id">编号</param>
        /// <returns>实体对象</returns>
        public static RedisNode FindById(Int32 id)
        {
            if (id <= 0) return null;

            // 实体缓存
            if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.Id == id);

            // 单对象缓存
            return Meta.SingleCache[id];

            //return Find(_.Id == id);
        }

        /// <summary>根据地址查找</summary>
        /// <param name="server">地址</param>
        /// <returns>实体对象</returns>
        public static RedisNode FindByServer(String server)
        {
            // 实体缓存
            if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.Server == server);

            return Find(_.Server == server);
        }
        #endregion

        #region 高级查询
        /// <summary>高级查询</summary>
        /// <param name="server">地址。含端口</param>
        /// <param name="category">分类</param>
        /// <param name="enable">启用</param>
        /// <param name="start">更新时间开始</param>
        /// <param name="end">更新时间结束</param>
        /// <param name="key">关键字</param>
        /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
        /// <returns>实体列表</returns>
        public static IList<RedisNode> Search(String server, String category, Boolean? enable, DateTime start, DateTime end, String key, PageParameter page)
        {
            var exp = new WhereExpression();

            if (!server.IsNullOrEmpty()) exp &= _.Server == server;
            if (!category.IsNullOrEmpty()) exp &= _.Category == category;
            if (enable != null) exp &= _.Enable == enable;

            exp &= _.UpdateTime.Between(start, end);
            if (!key.IsNullOrEmpty()) exp &= _.Name.Contains(key) | _.Category.Contains(key) | _.Password.Contains(key) | _.Version.Contains(key) | _.Mode.Contains(key) | _.MemoryPolicy.Contains(key) | _.MemoryAllocator.Contains(key) | _.CreateUser.Contains(key) | _.CreateIP.Contains(key) | _.UpdateUser.Contains(key) | _.UpdateIP.Contains(key) | _.Remark.Contains(key);

            return FindAll(exp, page);
        }

        // Select Count(Id) as Id,Category From RedisNode Where CreateTime>'2020-01-24 00:00:00' Group By Category Order By Id Desc limit 20
        private static readonly FieldCache<RedisNode> _CategoryCache = new FieldCache<RedisNode>(nameof(Category));

        /// <summary>获取类别列表，字段缓存10分钟，分组统计数据最多的前20种，用于魔方前台下拉选择</summary>
        /// <returns></returns>
        public static IDictionary<String, String> GetCategoryList() => _CategoryCache.FindAllName();
        #endregion

        #region 业务操作
        /// <summary>从Redis信息填充字段</summary>
        /// <param name="inf"></param>
        public void Fill(IDictionary<String, String> inf)
        {
            Version = inf["redis_version"];
            Mode = inf["redis_mode"];
            Role = inf["role"];

            MaxMemory = (Int32)(inf["maxmemory"].ToLong() / 1024 / 1024);
            if (MaxMemory == 0) MaxMemory = (Int32)(inf["total_system_memory"].ToLong() / 1024 / 1024);

            MemoryPolicy = inf["maxmemory_policy"];
            MemoryAllocator = inf["mem_allocator"];

            Remark = inf.ToJson().Substring(0, 500);
        }
        #endregion
    }
}