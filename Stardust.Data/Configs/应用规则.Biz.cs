using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using NewLife;
using NewLife.Log;
using XCode;
using XCode.Membership;

namespace Stardust.Data.Configs
{
    /// <summary>应用规则。针对应用设置的规则，比如根据IP段设置作用域</summary>
    public partial class AppRule : Entity<AppRule>
    {
        #region 对象操作
        static AppRule()
        {
            // 累加字段，生成 Update xx Set Count=Count+1234 Where xxx
            //var df = Meta.Factory.AdditionalFields;
            //df.Add(nameof(Priority));

            // 过滤器 UserModule、TimeModule、IPModule
            Meta.Modules.Add<UserModule>();
            Meta.Modules.Add<TimeModule>();
            Meta.Modules.Add<IPModule>();
        }

        /// <summary>首次连接数据库时初始化数据，仅用于实体类重载，用户不应该调用该方法</summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        protected override void InitData()
        {
            // InitData一般用于当数据表没有数据时添加一些默认数据，该实体类的任何第一次数据库操作都会触发该方法，默认异步调用
            if (Meta.Count > 0) return;

            if (XTrace.Debug) XTrace.WriteLine("开始初始化AppRule[应用规则]数据……");

            var entity = new AppRule
            {
                Rule = "IP=*",
                Result = "Scope=dev",
                Priority = -1,
                Remark = "全局默认开发",
            };
            entity.Insert();

            entity = new AppRule
            {
                Rule = "IP=172.*,10.*",
                Result = "Scope=pro",
                Priority = 10,
                Remark = "正式环境",
            };
            entity.Insert();

            //entity = new AppRule
            //{
            //    Rule = "IP=192.*",
            //    Result = "Scope=test",
            //    Priority = 20,
            //    Remark = "测试环境",
            //};
            //entity.Insert();

            entity = new AppRule
            {
                Rule = "LocalIP=127.*,::1,192.*",
                Result = "Scope=dev",
                Priority = 20,
                Remark = "本机开发",
            };
            entity.Insert();

            if (XTrace.Debug) XTrace.WriteLine("完成初始化AppRule[应用规则]数据！");
        }
        #endregion

        #region 扩展属性
        #endregion

        #region 扩展查询
        /// <summary>根据编号查找</summary>
        /// <param name="id">编号</param>
        /// <returns>实体对象</returns>
        public static AppRule FindById(Int32 id)
        {
            if (id <= 0) return null;

            // 实体缓存
            if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.Id == id);

            // 单对象缓存
            return Meta.SingleCache[id];

            //return Find(_.Id == id);
        }
        #endregion

        #region 高级查询

        // Select Count(Id) as Id,Category From AppRule Where CreateTime>'2020-01-24 00:00:00' Group By Category Order By Id Desc limit 20
        //static readonly FieldCache<AppRule> _CategoryCache = new FieldCache<AppRule>(nameof(Category))
        //{
        //Where = _.CreateTime > DateTime.Today.AddDays(-30) & Expression.Empty
        //};

        ///// <summary>获取类别列表，字段缓存10分钟，分组统计数据最多的前20种，用于魔方前台下拉选择</summary>
        ///// <returns></returns>
        //public static IDictionary<String, String> GetCategoryList() => _CategoryCache.FindAllName();
        #endregion

        #region 业务操作
        /// <summary>计算作用域</summary>
        /// <param name="appid"></param>
        /// <param name="ip">远程IP地址</param>
        /// <param name="localIp">本地IP地址</param>
        /// <returns></returns>
        public static String CheckScope(Int32 appid, String ip, String localIp)
        {
            if (ip.IsNullOrEmpty() && localIp.IsNullOrEmpty()) return null;

            var list = Meta.Cache.Entities.FindAll(e => e.Enable);
            list = list.Where(e =>
                !e.Rule.IsNullOrEmpty() && /*e.Rule.StartsWithIgnoreCase("IP=") &&*/
                !e.Result.IsNullOrEmpty() && e.Result.StartsWithIgnoreCase("Scope="))
                .OrderByDescending(e => e.Priority)
                .ToList();
            if (list.Count == 0) return null;

            var rule = list.Where(e => e.Match(ip, localIp)).OrderByDescending(e => e.Priority).ThenByDescending(e => e.Id).FirstOrDefault();
            if (rule == null) return null;

            var dic = rule.Result.SplitAsDictionary("=", ";");
            if (dic.TryGetValue("Scope", out var str)) return str;

            return null;
        }

        /// <summary>匹配规则</summary>
        /// <param name="ip">远程IP地址</param>
        /// <param name="localIp">本地IP地址</param>
        /// <returns></returns>
        public Boolean Match(String ip, String localIp)
        {
            var dic = Rule.SplitAsDictionary("=", ";");
            var rules = dic.ToDictionary(e => e.Key, e => e.Value.Split(","), StringComparer.OrdinalIgnoreCase);

            // 没有使用该规则，直接过
            if (rules.TryGetValue("ip", out var vs))
            {
                if (ip.IsNullOrEmpty() || !vs.Any(e => e.IsMatch(ip))) return false;
            }
            if (rules.TryGetValue("localIp", out vs))
            {
                if (localIp.IsNullOrEmpty() || !vs.Any(e => e.IsMatch(localIp))) return false;
            }

            return true;
        }
        #endregion
    }
}