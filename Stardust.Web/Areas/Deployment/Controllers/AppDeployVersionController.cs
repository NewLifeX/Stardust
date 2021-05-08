using System;
using System.Collections.Generic;
using System.ComponentModel;
using NewLife.Cube;
using NewLife.Web;
using Stardust.Data.Deployment;
using XCode;
using XCode.Membership;

namespace Stardust.Web.Areas.Deployment.Controllers
{
    [DeploymentArea]
    public class AppDeployVersionController : EntityController<AppDeployVersion>
    {
        static AppDeployVersionController()
        {
            MenuOrder = 80;

            ListFields.RemoveCreateField();

            AddFormFields.RemoveCreateField();

            LogOnChange = true;
        }

        protected override IEnumerable<AppDeployVersion> Search(Pager p)
        {
            var id = p["id"].ToInt(-1);
            if (id > 0)
            {
                var entity = AppDeployVersion.FindById(id);
                if (entity != null) return new List<AppDeployVersion> { entity };
            }

            var appId = p["appId"].ToInt(-1);
            var start = p["dtStart"].ToDateTime();
            var end = p["dtEnd"].ToDateTime();

            PageSetting.EnableAdd = appId > 0;
            PageSetting.EnableNavbar = false;

            return AppDeployVersion.Search(appId, null, null, start, end, p["Q"], p);
        }

        protected override Int32 OnInsert(AppDeployVersion entity)
        {
            var rs = base.OnInsert(entity);
            entity.App?.Fix();
            return rs;
        }
    }
}