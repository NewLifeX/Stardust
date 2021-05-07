using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using NewLife.Cube;
using NewLife.Web;
using Stardust.Data.Deployment;
using Stardust.Data.Nodes;
using XCode;
using XCode.Membership;

namespace Stardust.Web.Areas.Deployment.Controllers
{
    [DeploymentArea]
    public class AppDeployController : EntityController<AppDeploy>
    {
        private readonly StarFactory _starFactory;

        static AppDeployController()
        {
            MenuOrder = 90;

            ListFields.RemoveCreateField();
            ListFields.RemoveField("ApolloMetaServer");

            {
                var df = ListFields.AddDataField("AddNode", null, "Enable");
                df.Header = "节点";
                df.DisplayName = "添加节点";
                df.Title = "添加服务器节点";
                df.Url = "AppDeployNode/Add?appId={Id}";
            }

            {
                var df = ListFields.AddDataField("Nodes");
                df.Header = "节点";
                //df.DisplayName = "添加节点";
                //df.Title = "添加服务器节点";
                df.Url = "AppDeployNode?appId={Id}";
            }

            {
                var df = ListFields.AddDataField(AppDeploy._.Name);
                //df.Header = "应用";
                df.Url = "/Registry/App?q={Name}";
            }

            {
                var df = ListFields.AddDataField("Log", "UpdateUserId");
                df.DisplayName = "修改日志";
                df.Header = "修改日志";
                df.Url = "/Admin/Log?category=应用部署&linkId={Id}";
            }
        }

        public AppDeployController(StarFactory starFactory) => _starFactory = starFactory;

        protected override IEnumerable<AppDeploy> Search(Pager p)
        {
            var id = p["id"].ToInt(-1);
            if (id > 0)
            {
                var entity = AppDeploy.FindById(id);
                if (entity != null) return new List<AppDeploy> { entity };
            }

            var category = p["category"];
            var enable = p["enable"]?.ToBoolean();

            var start = p["dtStart"].ToDateTime();
            var end = p["dtEnd"].ToDateTime();

            return AppDeploy.Search(category, enable, start, end, p["Q"], p);
        }

        protected override Boolean Valid(AppDeploy entity, DataObjectMethodType type, Boolean post)
        {
            if (!post)
            {
                if (type == DataObjectMethodType.Insert)
                {
                    entity.Enable = true;
                    entity.AutoStart = true;
                }

                return base.Valid(entity, type, post);
            }

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

        protected override Int32 OnUpdate(AppDeploy entity)
        {
            // 如果执行了启用，则通知节点
            if ((entity as IEntity).Dirtys["Enable"]) Task.Run(() => NotifyChange(entity.Id));

            return base.OnUpdate(entity);
        }

        public async Task<ActionResult> NotifyChange(Int32 id)
        {
            var deploy = AppDeploy.FindById(id);
            if (deploy != null)
            {
                // 通知该发布集之下所有节点，应用服务数据有变化，需要马上执行心跳
                var list = AppDeployNode.FindAllByAppId(deploy.Id);
                foreach (var item in list)
                {
                    var node = item.Node;
                    if (node != null)
                    {
                        // 通过接口发送指令给StarServer
                        await _starFactory.SendNodeCommand(node.Code, "Deploy");
                    }
                }
            }

            return RedirectToAction("Index");
        }
    }
}