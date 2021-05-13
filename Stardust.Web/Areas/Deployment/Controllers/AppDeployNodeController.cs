using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using NewLife.Cube;
using NewLife.Web;
using Stardust.Data.Deployment;
using XCode;
using XCode.Membership;

namespace Stardust.Web.Areas.Deployment.Controllers
{
    [DeploymentArea]
    public class AppDeployNodeController : EntityController<AppDeployNode>
    {
        static AppDeployNodeController()
        {
            MenuOrder = 88;

            ListFields.RemoveCreateField();
            AddFormFields.RemoveCreateField();

            LogOnChange = true;
        }

        private readonly StarFactory _starFactory;
        public AppDeployNodeController(StarFactory starFactory) => _starFactory = starFactory;

        protected override IEnumerable<AppDeployNode> Search(Pager p)
        {
            var id = p["id"].ToInt(-1);
            if (id > 0)
            {
                var entity = AppDeployNode.FindById(id);
                if (entity != null) return new List<AppDeployNode> { entity };
            }

            var appId = p["appId"].ToInt(-1);
            var nodeId = p["nodeId"].ToInt(-1);

            PageSetting.EnableAdd = appId > 0;
            PageSetting.EnableNavbar = false;

            return AppDeployNode.Search(appId, nodeId, p["Q"], p);
        }

        protected override Boolean Valid(AppDeployNode entity, DataObjectMethodType type, Boolean post)
        {
            if (!post) return base.Valid(entity, type, post);

            entity.App?.Fix();

            return base.Valid(entity, type, post);
        }

        /// <summary>执行操作</summary>
        /// <param name="act"></param>
        /// <param name="id"></param>
        /// <returns></returns>
        [EntityAuthorize(PermissionFlags.Update)]
        public async Task<ActionResult> Operate(String act, Int32 id)
        {
            var dn = AppDeployNode.FindById(id);
            if (dn == null || dn.Node == null || dn.App == null) return Json(500, $"[{id}]不存在");

            await _starFactory.SendNodeCommand(dn.Node.Code, act, dn.AppName);

            return JsonRefresh($"在节点[{dn.Node}]上对应用[{dn.App}]执行[{act}]操作", 3);
        }
    }
}