using System;
using System.ComponentModel;
using Microsoft.AspNetCore.Mvc;
using NewLife.Cube;
using Stardust.Data;
using XCode;
using XCode.Membership;
using static Stardust.Data.Service;

namespace Stardust.Web.Areas.Registry.Controllers
{
    [RegistryArea]
    public class ServiceInfoController : EntityController<Service>
    {
        static ServiceInfoController()
        {
            MenuOrder = 80;

            ListFields.RemoveField("Secret");
          
            {
                var df = ListFields.AddDataField(_.Providers);
                df.Url = "AppService?serviceId={Id}";
            }
            {
                var df = ListFields.AddDataField(_.Consumers);
                df.Url = "AppConsume?serviceId={Id}";
            }
        }

        protected override Boolean Valid(Service entity, DataObjectMethodType type, Boolean post)
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
    }
}