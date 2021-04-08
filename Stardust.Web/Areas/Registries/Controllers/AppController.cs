using System;
using System.ComponentModel;
using Microsoft.AspNetCore.Mvc;
using NewLife.Cube;
using Stardust.Data;
using XCode;
using XCode.Membership;

namespace Stardust.Web.Areas.Registries.Controllers
{
    [RegistryArea]
    public class AppController : EntityController<App>
    {
        static AppController()
        {
            MenuOrder = 99;

            ListFields.RemoveField("Secret");
          
            {
                var df = ListFields.AddDataField("History", null, "AutoActive");
                df.DisplayName = "历史";
                df.Header = "历史";
                df.Url = "AppHistory?appId={Id}";
            }
            {
                var df = ListFields.AddDataField("Providers", null, "AutoActive");
                df.DisplayName = "提供服务";
                df.Header = "提供服务";
                df.Url = "AppService?appId={Id}";
                df.DataVisible = (e, f) => (e as App).Providers.Count > 0;
            }
            {
                var df = ListFields.AddDataField("Consumers", null, "AutoActive");
                df.DisplayName = "消费服务";
                df.Header = "消费服务";
                df.Url = "AppConsume?appId={Id}";
                df.DataVisible = (e, f) => (e as App).Consumers.Count > 0;
            }
            {
                var df = ListFields.AddDataField("Log", "CreateUser");
                df.DisplayName = "修改日志";
                df.Header = "修改日志";
                df.Url = "/Admin/Log?category=应用系统&linkId={Id}";
            }
        }

        protected override Boolean Valid(App entity, DataObjectMethodType type, Boolean post)
        {
            if (!post) return base.Valid(entity, type, post);

            // 必须提前写修改日志，否则修改后脏数据失效，保存的日志为空
            if (type == DataObjectMethodType.Update && (entity as IEntity).HasDirty)
                LogProvider.Provider.WriteLog(type + "", entity);

            var err = "";
            try
            {
                return base.Valid(entity, type, post);
            }
            catch (Exception ex)
            {
                err = ex.Message;
                throw;
            }
            finally
            {
                if (type != DataObjectMethodType.Update) LogProvider.Provider.WriteLog(type + "", entity, err);
            }
        }

        /// <summary>启用禁用下线告警</summary>
        /// <param name="enable"></param>
        /// <returns></returns>
        [EntityAuthorize(PermissionFlags.Update)]
        public ActionResult SetAlarm(Boolean enable = true)
        {
            foreach (var item in SelectKeys)
            {
                var dt = App.FindById(item.ToInt());
                if (dt != null)
                {
                    dt.AlarmOnOffline = enable;
                    dt.Save();
                }
            }

            return JsonRefresh("操作成功！");
        }
    }
}