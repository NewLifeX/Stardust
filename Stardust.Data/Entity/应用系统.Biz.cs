using System;
using NewLife;
using Stardust.Monitors;
using XCode.Membership;

namespace Stardust.Data
{
    /// <summary>应用系统。服务提供者和消费者</summary>
    public partial class App : EntityBase<App>
    {
        #region 对象操作
        static App()
        {
            // 累加字段
            //var df = Meta.Factory.AdditionalFields;
            //df.Add(__.Services);

            // 过滤器 UserModule、TimeModule、IPModule
            Meta.Modules.Add<UserModule>();
            Meta.Modules.Add<TimeModule>();
            Meta.Modules.Add<IPModule>();

            // 单对象缓存
            var sc = Meta.SingleCache;
            sc.FindSlaveKeyMethod = k => Find(__.Name, k);
            sc.GetSlaveKeyMethod = e => e.Name;
        }

        /// <summary>验证数据，通过抛出异常的方式提示验证失败。</summary>
        /// <param name="isNew">是否插入</param>
        public override void Valid(Boolean isNew)
        {
            // 如果没有脏数据，则不需要进行任何处理
            if (!HasDirty) return;

            // 这里验证参数范围，建议抛出参数异常，指定参数名，前端用户界面可以捕获参数异常并聚焦到对应的参数输入框
            if (Name.IsNullOrEmpty()) throw new ArgumentNullException(nameof(Name), "名称不能为空！");

            if (isNew && !Dirtys[__.AutoActive]) AutoActive = true;
        }
        #endregion

        #region 扩展属性
        #endregion

        #region 扩展查询
        /// <summary>根据编号查找</summary>
        /// <param name="id">编号</param>
        /// <returns>实体对象</returns>
        public static App FindByID(Int32 id)
        {
            if (id <= 0) return null;

            // 实体缓存
            if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.ID == id);

            // 单对象缓存
            return Meta.SingleCache[id];

            //return Find(_.ID == id);
        }

        /// <summary>根据名称查找</summary>
        /// <param name="name">名称</param>
        /// <returns>实体对象</returns>
        public static App FindByName(String name)
        {
            if (name.IsNullOrEmpty()) return null;

            // 实体缓存
            if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.Name.EqualIgnoreCase(name));

            // 单对象缓存
            //return Meta.SingleCache.GetItemWithSlaveKey(name) as App;

            return Find(_.Name == name);
        }
        #endregion

        #region 高级查询
        #endregion

        #region 业务操作
        /// <summary>更新信息</summary>
        /// <param name="model"></param>
        /// <param name="ip"></param>
        public static void UpdateInfo(TraceModel model, String ip)
        {
            // 修复数据
            var app = FindByName(model.AppId);
            if (app == null) app = new App { Name = model.AppId, DisplayName = model.AppName, Enable = true };
            if (app.DisplayName.IsNullOrEmpty()) app.DisplayName = model.AppName;
            app.Save();

            // 更新应用信息
            if (app != null && model.Info != null)
            {
                var clientId = model.ClientId ?? ip;
                var ss = clientId.Split('@');
                if (ss.Length == 3) clientId = $"{ss[0]}@{ss[1]}";

                var online = Data.AppOnline.GetOrAddSession(clientId);
                online.Version = model.Version;
                online.UpdateInfo(app, model.Info);

                // 优先使用clientId内部的内网本机IP作为跟踪数据客户端实例
                if (!model.ClientId.IsNullOrEmpty() && model.ClientId.Contains("@")) ip = model.ClientId.Substring(null, "@");
                AppMeter.WriteData(app, model.Info, ip);
            }
        }

        /// <summary>写应用历史</summary>
        /// <param name="action"></param>
        /// <param name="success"></param>
        /// <param name="remark"></param>
        public void WriteHistory(String action, Boolean success, String remark)
        {
            var history = AppHistory.Create(this, action, success, remark);

            history.SaveAsync();
        }
        #endregion
    }
}