﻿using System;
using System.ComponentModel;
using NewLife.Cube;
using Stardust.Data.Configs;
using XCode;
using XCode.Membership;

namespace Stardust.Web.Areas.Configs.Controllers
{
    [ConfigsArea]
    public class AppRuleController : EntityController<AppRule>
    {
        static AppRuleController()
        {
            {
                var df = ListFields.AddDataField("Log", "CreateUserID");
                df.DisplayName = "修改日志";
                df.Header = "修改日志";
                df.Url = "/Admin/Log?category=应用规则&linkId={Id}";
            }
        }

        //protected override IEnumerable<AppRule> Search(Pager p)
        //{
        //    var appId = p["appId"].ToInt(-1);

        //    var start = p["dtStart"].ToDateTime();
        //    var end = p["dtEnd"].ToDateTime();

        //    return AppRule.Search(appId, start, end, p["Q"], p);
        //}

        protected override Boolean Valid(AppRule entity, DataObjectMethodType type, Boolean post)
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